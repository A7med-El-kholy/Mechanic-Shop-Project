namespace MechanicShop.Application.Features.AI;

using MechanicShop.Application.Features.AI.DTOs;

/// <summary>
/// Abstraction over the AI diagnostic engine. The default implementation
/// (MockAIService) returns realistic, rule-based results so the feature works
/// end-to-end without any external API key. Swap in OpenAIService /
/// AzureOpenAIService / GeminiService when credentials are configured.
/// </summary>
public interface IAIService
{
    /// <summary>Text-based vehicle diagnosis.</summary>
    Task<DiagnoseResponse> DiagnoseTextAsync(DiagnoseTextRequest request, CancellationToken ct = default);

    /// <summary>Image-based vehicle diagnosis.</summary>
    Task<DiagnoseResponse> DiagnoseImageAsync(DiagnoseImageRequest request, CancellationToken ct = default);

    /// <summary>Speech-to-text transcription of an audio recording.</summary>
    Task<TranscribeResponse> TranscribeAudioAsync(TranscribeAudioRequest request, CancellationToken ct = default);

    /// <summary>Text-to-speech synthesis returning base64 audio.</summary>
    Task<SpeakResponse> SpeakTextAsync(SpeakTextRequest request, CancellationToken ct = default);

    /// <summary>Available synthesis voices for the voice picker.</summary>
    Task<IReadOnlyList<VoiceDto>> GetVoicesAsync(CancellationToken ct = default);
}
