namespace MechanicShop.Api.Features.AI;

using MechanicShop.Api.Common;
using MechanicShop.Application.Features.AI;
using MechanicShop.Application.Features.AI.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

/// <summary>
/// AI diagnostic surface: text diagnosis, image diagnosis, speech-to-text,
/// and text-to-speech. All endpoints require a bearer token so every AI
/// interaction is tied to an authenticated customer or staff member.
/// </summary>
public static class AIEndpoints
{
    public static IEndpointRouteBuilder MapAIEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ai").WithTags("AI").RequireAuthorization();

        group.MapPost("/diagnose", DiagnoseTextAsync)
            .WithName("DiagnoseText")
            .Produces<DiagnoseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/diagnose-image", DiagnoseImageAsync)
            .WithName("DiagnoseImage")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<DiagnoseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge);

        group.MapPost("/transcribe", TranscribeAudioAsync)
            .WithName("TranscribeAudio")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<TranscribeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge);

        group.MapPost("/speak", SpeakTextAsync)
            .WithName("SpeakText")
            .Produces<SpeakResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/voices", GetVoicesAsync)
            .WithName("GetVoices")
            .Produces<List<VoiceDto>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> DiagnoseTextAsync(
        DiagnoseTextRequest request,
        IAIService aiService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Error.Validation("AI.EmptyDescription", "Please describe the symptom.")
                .ToProblem(httpContext);
        }

        return await HandleProviderCallAsync(async () =>
            Results.Ok(await aiService.DiagnoseTextAsync(request, cancellationToken)), cancellationToken);
    }

    private static async Task<IResult> DiagnoseImageAsync(
        IFormFile image,
        string? description,
        string? vehicleId,
        IAIService aiService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return Error.Validation("AI.NoImage", "Please upload an image.")
                .ToProblem(httpContext);
        }

        // 10 MB cap — adjust in production via configuration.
        const long MaxBytes = 10L * 1024 * 1024;
        if (image.Length > MaxBytes)
        {
            return Error.Validation("AI.ImageTooLarge", "Image must be under 10 MB.")
                .ToProblem(httpContext);
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var supported = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"], [".webp"] = ["image/webp"],
        };
        if (!supported.TryGetValue(extension, out var contentTypes) ||
            !contentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Error.Validation("AI.ImageFormatUnsupported", "Upload a JPG, JPEG, PNG, or WEBP image.")
                .ToProblem(httpContext);
        }

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, cancellationToken);
        if (!HasImageSignature(ms.ToArray(), image.ContentType))
        {
            return Error.Validation("AI.InvalidImage", "The uploaded file is not a valid image of the selected format.")
                .ToProblem(httpContext);
        }

        return await HandleProviderCallAsync(async () => Results.Ok(await aiService.DiagnoseImageAsync(
            new DiagnoseImageRequest(image.FileName, image.ContentType, ms.ToArray(), description, vehicleId),
            cancellationToken)), cancellationToken);
    }

    private static bool HasImageSignature(byte[] bytes, string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 &&
            System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
        _ => false,
    };

    private static async Task<IResult> TranscribeAudioAsync(
        IFormFile audio,
        IAIService aiService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length == 0)
        {
            return Error.Validation("AI.NoAudio", "Please upload an audio recording.")
                .ToProblem(httpContext);
        }

        const long MaxBytes = 25L * 1024 * 1024;
        if (audio.Length > MaxBytes)
        {
            return Error.Validation("AI.AudioTooLarge", "Audio must be under 25 MB.")
                .ToProblem(httpContext);
        }

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms, cancellationToken);

        return await HandleProviderCallAsync(async () => Results.Ok(await aiService.TranscribeAudioAsync(
            new TranscribeAudioRequest(audio.FileName, audio.ContentType, ms.ToArray()),
            cancellationToken)), cancellationToken);
    }

    private static async Task<IResult> SpeakTextAsync(
        SpeakTextRequest request,
        IAIService aiService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Error.Validation("AI.EmptyText", "Nothing to speak.")
                .ToProblem(httpContext);
        }

        return await HandleProviderCallAsync(async () => Results.Ok(await aiService.SpeakTextAsync(request, cancellationToken)), cancellationToken);
    }

    private static async Task<IResult> GetVoicesAsync(
        IAIService aiService,
        CancellationToken cancellationToken)
    {
        var voices = await aiService.GetVoicesAsync(cancellationToken);
        return Results.Ok(voices);
    }

    private static async Task<IResult> HandleProviderCallAsync(Func<Task<IResult>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return Results.Problem("The AI provider timed out. Please try again.", statusCode: StatusCodes.Status504GatewayTimeout,
                title: "AI provider timeout");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            return Results.Problem("The AI provider is unavailable or returned an invalid response. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable, title: "AI service unavailable");
        }
    }
}
