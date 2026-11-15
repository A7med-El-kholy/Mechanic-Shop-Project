namespace MechanicShop.Application.Features.AI.DTOs;

/// <summary>
/// Text-based diagnostic request. The client sends a free-form description of
/// the vehicle symptom plus an optional vehicle id. The AI returns a structured
/// diagnosis: likely causes, urgency, cost range, safety advisory, and parts.
/// </summary>
public sealed record DiagnoseTextRequest(
    string Description,
    string? VehicleId,
    string? Language);

/// <summary>
/// Image-based diagnostic request. The client uploads a photo of the vehicle,
/// a warning light, a part, or a leak, plus an optional description.
/// </summary>
public sealed record DiagnoseImageRequest(
    string FileName,
    string ContentType,
    byte[] ImageData,
    string? Description,
    string? VehicleId);

/// <summary>
/// Speech-to-text request. The client uploads an audio recording (WebM/OPUS or
/// WAV from the browser MediaRecorder) and the service returns the transcript.
/// </summary>
public sealed record TranscribeAudioRequest(
    string FileName,
    string ContentType,
    byte[] AudioData);

/// <summary>
/// Text-to-speech request. The service synthesizes the given text into audio
/// and returns it as a playable audio blob (MP3/WAV) plus a base64 fallback.
/// </summary>
public sealed record SpeakTextRequest(
    string Text,
    string? Voice,
    string? Language);
