namespace MechanicShop.Application.Features.AI.DTOs;

/// <summary>
/// One probable cause returned by the AI diagnostic engine.
/// </summary>
public sealed record ProbableCauseDto(
    string Name,
    string ProbabilityPercent,
    string Description);

/// <summary>
/// Structured diagnostic result returned to the client for both text and
/// image-based requests. The UI renders each field in the HUD panel.
/// </summary>
public sealed record DiagnosticResultDto(
    string Title,
    string Urgency,              // Low | Medium | High | Critical
    string UrgencyColor,
    string EstimatedCost,
    string EstimatedLaborTime,
    bool SafeToDrive,
    string DrivingAdvice,
    IReadOnlyList<ProbableCauseDto> LikelyCauses,
    IReadOnlyList<string> RecommendedActions,
    IReadOnlyList<string> PartsNeeded);

/// <summary>
/// Response for /api/ai/diagnose (text) and /api/ai/diagnose-image.
/// </summary>
public sealed record DiagnoseResponse(
    DiagnosticResultDto Diagnosis,
    string Transcript,           // echoed back when voice was used
    string ResponseText,         // human-readable summary for TTS
    string RequestId);

/// <summary>
/// Response for /api/ai/transcribe.
/// </summary>
public sealed record TranscribeResponse(
    string Transcript,
    string RequestId);

/// <summary>
/// Response for /api/ai/speak. Audio is base64-encoded so the client can play
/// it directly without a second round-trip.
/// </summary>
public sealed record SpeakResponse(
    string AudioBase64,
    string ContentType,
    string RequestId);

/// <summary>
/// Available voice metadata for the TTS voice picker.
/// </summary>
public sealed record VoiceDto(
    string Id,
    string Name,
    string Language);
