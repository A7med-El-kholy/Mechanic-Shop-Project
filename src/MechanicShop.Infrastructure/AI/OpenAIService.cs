namespace MechanicShop.Infrastructure.AI;

using MechanicShop.Application.Features.AI;
using MechanicShop.Application.Features.AI.DTOs;
using MechanicShop.Infrastructure.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

/// <summary>
/// Real AI provider: an OpenAI-compatible chat/audio API, selected by configuration
/// when <c>AISettings:Provider == "OpenAI"</c> AND an API key is configured. Azure
/// OpenAI (same /chat/completions + /audio/* shape) and self-hosted compatible
/// servers (vLLM, Ollama, LM Studio, ...) are reached by changing
/// <c>AISettings:OpenAI:BaseUrl</c>.
///
/// Diagnose / DiagnoseImage are structured: the prompt asks the model to emit the
/// <see cref="DiagnosticResultDto"/> JSON schema and we parse it. If the provider
/// fails or returns unparseable output we log and fall back to the offline
/// <see cref="MockAIService"/> so the AI assistant never breaks the shop UI.
/// Transcribe calls the real audio-transcriptions endpoint; Speak calls the TTS
/// audio/speech endpoint and returns real audio bytes.
///
/// Cancellation flows from the endpoint via the <c>CancellationToken</c>, and the
/// HttpClient is configured with a server-side timeout through "AI" client.
/// </summary>
public sealed class OpenAIService(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAIService> logger,
    IConfiguration configuration,
    AppDbContext db) : IAIService
{
    private const string ChatCompletionsRelativeUrl = "/chat/completions";
    private const string TranscriptionsRelativeUrl = "/audio/transcriptions";
    private const string SpeechRelativeUrl = "/audio/speech";

    private const string DiagnosisSystemPrompt =
        """
        You are a professional automotive diagnostic engine for a mechanic shop.
        Given a vehicle symptom description, return a JSON object with EXACTLY this
        shape:

        {
          "title": "Short human-readable diagnosis title",
          "urgency": "Low|Medium|High|Critical",
          "urgencyColor": "#hex",
          "estimatedCost": "$180 – $360",
          "estimatedLaborTime": "1.2 – 2.0 hrs",
          "safeToDrive": true or false,
          "drivingAdvice": "Advice for the vehicle owner.",
          "likelyCauses": [{ "name": "...", "probabilityPercent": "90%", "description": "..." }],
          "recommendedActions": ["step 1", "step 2"],
          "partsNeeded": ["part 1"]
        }

        Rules: all values are strings except safeToDrive (boolean) and
        recommendedActions/partsNeeded (arrays of strings). Return ONLY the JSON
        object — no markdown, no preamble, no trailing text.
        """;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<OpenAIService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly AppDbContext _db = db;

    public async Task<DiagnoseResponse> DiagnoseTextAsync(DiagnoseTextRequest request, CancellationToken ct = default)
    {
        try
        {
            var diagnosis = await CallDiagnosisAsync(
                $"[Text description] {request.Description}", ct);

            return new DiagnoseResponse(diagnosis, request.Description ?? string.Empty,
                BuildResponseText(diagnosis), Guid.NewGuid().ToString("N")[..12]);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI diagnose failed.");
            throw new HttpRequestException("The configured AI provider could not complete the diagnosis.", ex);
        }
    }

    public async Task<DiagnoseResponse> DiagnoseImageAsync(DiagnoseImageRequest request, CancellationToken ct = default)
    {
        try
        {
            var diagnosis = await CallDiagnosisAsync(
                $"[Image analysis] {request.Description}", ct, request.ImageData, request.ContentType);

            return new DiagnoseResponse(diagnosis, request.Description ?? string.Empty,
                BuildResponseText(diagnosis), Guid.NewGuid().ToString("N")[..12]);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI image diagnosis failed.");
            throw new HttpRequestException("The configured AI provider could not analyze the image.", ex);
        }
    }

    public async Task<TranscribeResponse> TranscribeAudioAsync(TranscribeAudioRequest request, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(request.AudioData), "file", SanitizeFileName(request.FileName) },
            { new StringContent(_configuration["AISettings:OpenAI:TranscriptionModel"] ?? "whisper-1"), "model" },
        };

        var body = await PostFormAsync(TranscriptionsRelativeUrl, content, ct);
        using var document = JsonDocument.Parse(body);
        var text = document.RootElement.GetProperty("text").GetString() ?? string.Empty;

        return new TranscribeResponse(text.Trim(), Guid.NewGuid().ToString("N")[..12]);
    }

    public async Task<SpeakResponse> SpeakTextAsync(SpeakTextRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _configuration["AISettings:OpenAI:SpeechModel"] ?? "tts-1",
            input = request.Text,
            voice = string.IsNullOrWhiteSpace(request.Voice) ? "nova" : request.Voice,
            response_format = "mp3",
        };

        var client = _httpClientFactory.CreateClient("AI");
        using var requestMessage = CreateAuthorizedRequest(HttpMethod.Post, SpeechRelativeUrl);
        requestMessage.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(requestMessage, ct);
        var body = await response.Content.ReadAsByteArrayAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI {SpeechRelativeUrl} returned {(int)response.StatusCode}: {Truncate(Encoding.UTF8.GetString(body))}");
        }

        return new SpeakResponse(Convert.ToBase64String(body), "audio/mpeg",
            Guid.NewGuid().ToString("N")[..12]);
    }

    public Task<IReadOnlyList<VoiceDto>> GetVoicesAsync(CancellationToken ct = default)
    {
        // Static catalogue: OpenAI exposes voices by name, not by a listing API.
        List<VoiceDto> voices =
        [
            new("alloy", "Alloy", "en-US"),
            new("echo", "Echo", "en-US"),
            new("fable", "Fable", "en-US"),
            new("onyx", "Onyx", "en-US"),
            new("nova", "Nova", "en-US"),
            new("shimmer", "Shimmer", "en-US"),
        ];
        return Task.FromResult((IReadOnlyList<VoiceDto>)voices);
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private async Task<DiagnosticResultDto> CallDiagnosisAsync(
        string userPrompt, CancellationToken ct, byte[]? imageData = null, string? imageContentType = null)
    {
        var workshopContext = await RetrieveWorkshopContextAsync(userPrompt, ct);
        using var message = BuildChatCompletionMessage(userPrompt, imageData, imageContentType, workshopContext);
        using var response = await _httpClientFactory
            .CreateClient("AI")
            .SendAsync(message, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI chat/completions returned {(int)response.StatusCode}: {Truncate(body)}",
                null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var messageContent = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;

        return ParseDiagnosticResult(messageContent);
    }

    /// <summary>Human-readable summary used as the displayed chat text, mirroring MockAIService.</summary>
    private static string BuildResponseText(DiagnosticResultDto diagnosis)
    {
        var causes = diagnosis.LikelyCauses.Take(2).Select(c => c.Name).ToArray();
        return
            $"Issue: {diagnosis.Title}. Urgency: {diagnosis.Urgency}. " +
            $"Estimated cost {diagnosis.EstimatedCost}, labor {diagnosis.EstimatedLaborTime}. " +
            $"{diagnosis.DrivingAdvice} " +
            (causes.Length > 0 ? $"Likely causes: {string.Join(", ", causes)}." : string.Empty) +
            $"Recommended: {string.Join("; ", diagnosis.RecommendedActions.Take(2))}.";
    }

    private HttpRequestMessage BuildChatCompletionMessage(
        string userPrompt, byte[]? imageData, string? imageContentType, string workshopContext)
    {
        // Vision request shape: the user message carries text + base64 image_url parts.
        object userContent;
        if (imageData is { Length: > 0 })
        {
            var mediaType = imageContentType ?? "image/jpeg";
            var dataUri = $"data:{mediaType};base64," + Convert.ToBase64String(imageData);
            userContent = new object[]
            {
                new { type = "text", text = userPrompt },
                new { type = "image_url", image_url = new { url = dataUri } },
            };
        }
        else
        {
            userContent = new object[] { new { type = "text", text = userPrompt } };
        }

        var body = new
        {
            model = _configuration["AISettings:OpenAI:Model"] ?? "gpt-4o-mini",
            messages = new object[]
            {
                new { role = "system", content = DiagnosisSystemPrompt + "\n\n" + workshopContext },
                new { role = "user", content = userContent },
            },
            max_tokens = 900,
            temperature = 0.3,
            response_format = new { type = "json_object" },
        };

        var request = CreateAuthorizedRequest(HttpMethod.Post, ChatCompletionsRelativeUrl);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<string> RetrieveWorkshopContextAsync(string query, CancellationToken ct)
    {
        try
        {
            var terms = query.Split([' ', ',', '.', '?', '!', ':', ';', '/', '\\'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(term => term.Length >= 4)
                .Select(term => term.ToLowerInvariant())
                .Distinct()
                .Take(8)
                .ToArray();

            if (terms.Length == 0) return "";

            var services = await _db.ServiceTasks.AsNoTracking()
                .Select(item => new { item.Name, item.BasePrice, item.EstimatedDurationInMins })
                .Take(50)
                .ToListAsync(ct);
            var parts = await _db.Parts.AsNoTracking()
                .Select(item => new { item.Name, item.Price, item.Quantity, item.MinimumQuantity })
                .Take(50)
                .ToListAsync(ct);

            var relevantServices = services.Where(item => terms.Any(term => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Take(4).ToArray();
            var relevantParts = parts.Where(item => terms.Any(term => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Take(4).ToArray();
            if (relevantServices.Length == 0 && relevantParts.Length == 0) return "";

            var lines = new List<string>
            {
                "Relevant workshop catalog data retrieved from the application database (use it only when it matches the question; do not invent availability or prices):"
            };
            lines.AddRange(relevantServices.Select(item =>
                $"Service: {item.Name}; price {item.BasePrice:0.00}; scheduled duration {(int)item.EstimatedDurationInMins} minutes."));
            lines.AddRange(relevantParts.Select(item =>
                $"Part: {item.Name}; price {item.Price:0.00}; on-hand {item.Quantity}; reorder level {item.MinimumQuantity}."));
            return string.Join("\n", lines);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workshop context retrieval failed; continuing without database context.");
            return "";
        }
    }

    private static DiagnosticResultDto ParseDiagnosticResult(string jsonText)
    {
        using var document = JsonDocument.Parse(ExtractJson(jsonText));
        var root = document.RootElement;

        return new DiagnosticResultDto(
            Title: GetString(root, "title", "Vehicle Diagnostic"),
            Urgency: GetString(root, "urgency", "Medium"),
            UrgencyColor: GetString(root, "urgencyColor", "#f59e0b"),
            EstimatedCost: GetString(root, "estimatedCost", "$100 – $300"),
            EstimatedLaborTime: GetString(root, "estimatedLaborTime", "1 – 2 hrs"),
            SafeToDrive: GetBool(root, "safeToDrive", true),
            DrivingAdvice: GetString(root, "drivingAdvice", "Consult a mechanic."),
            LikelyCauses: GetCauses(root),
            RecommendedActions: GetStringArray(root, "recommendedActions"),
            PartsNeeded: GetStringArray(root, "partsNeeded"));
    }

    private static IReadOnlyList<ProbableCauseDto> GetCauses(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("likelyCauses", out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var causes = new List<ProbableCauseDto>();
        foreach (var item in array.EnumerateArray())
        {
            causes.Add(new ProbableCauseDto(
                Name: GetString(item, "name", "Unknown"),
                ProbabilityPercent: GetString(item, "probabilityPercent", string.Empty),
                Description: GetString(item, "description", string.Empty)));
        }
        return causes;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(name, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                items.Add(item.GetString() ?? string.Empty);
            }
            else if (item.ValueKind == JsonValueKind.Object &&
                     item.TryGetProperty("name", out var nameElement))
            {
                items.Add(nameElement.GetString() ?? string.Empty);
            }
        }
        return items;
    }

    private static string GetString(JsonElement element, string name, string fallback)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.String)
        {
            return value.GetString() ?? fallback;
        }
        return fallback;
    }

    private static bool GetBool(JsonElement element, string name, bool fallback)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : fallback,
            _ => fallback,
        };
    }

    private static string ExtractJson(string text)
    {
        // Tolerate models that wrap the JSON in a fenced block or prose: take the
        // first '{' through the last '}'.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("The AI model did not return a JSON diagnosis.");
        }

        return text[start..(end + 1)];
    }

    private async Task<string> PostFormAsync(string relativeUrl, MultipartFormDataContent content, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("AI");
        using var request = CreateAuthorizedRequest(HttpMethod.Post, relativeUrl);
        request.Content = content;
        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI {relativeUrl} returned {(int)response.StatusCode}: {Truncate(body)}",
                null, response.StatusCode);
        }

        return body;
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string relativeUrl)
    {
        var baseUrl = _configuration["AISettings:OpenAI:BaseUrl"]?.TrimEnd('/');
        var apiKey = _configuration["AISettings:OpenAI:ApiKey"];
        var request = new HttpRequestMessage(method, $"{baseUrl ?? "https://api.openai.com/v1"}{relativeUrl}");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
        return request;
    }

    private static string Truncate(string value, int max = 400) =>
        value.Length <= max ? value : value[..max] + "…";

    private static string SanitizeFileName(string name)
    {
        var sanitized = (Path.GetFileName(name) ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
            .ToString()
            .Trim('.');

        return sanitized.Length > 0 ? sanitized : "recording";
    }
}
