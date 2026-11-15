namespace MechanicShop.Api.Features.AI;

using MechanicShop.Api.Common;
using MechanicShop.Application.Features.AI;
using MechanicShop.Application.Features.AI.DTOs;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

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
            .Produces<DiagnoseResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge);

        group.MapPost("/transcribe", TranscribeAudioAsync)
            .WithName("TranscribeAudio")
            .Accepts<IFormFile>("multipart/form-data")
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

        var result = await aiService.DiagnoseTextAsync(request, cancellationToken);
        return Results.Ok(result);
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

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, cancellationToken);

        var result = await aiService.DiagnoseImageAsync(
            new DiagnoseImageRequest(image.FileName, image.ContentType, ms.ToArray(), description, vehicleId),
            cancellationToken);

        return Results.Ok(result);
    }

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

        var result = await aiService.TranscribeAudioAsync(
            new TranscribeAudioRequest(audio.FileName, audio.ContentType, ms.ToArray()),
            cancellationToken);

        return Results.Ok(result);
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

        var result = await aiService.SpeakTextAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetVoicesAsync(
        IAIService aiService,
        CancellationToken cancellationToken)
    {
        var voices = await aiService.GetVoicesAsync(cancellationToken);
        return Results.Ok(voices);
    }
}
