namespace MechanicShop.Application.Features.Identity.Dtos;

/// <summary>Payload returned to the client after a successful login.</summary>
public sealed record TokenResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    AppUserDto User);
