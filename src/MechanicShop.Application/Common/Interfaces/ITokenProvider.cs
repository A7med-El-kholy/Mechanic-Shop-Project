using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

/// <summary>
/// Issues and validates JSON Web Tokens. Abstracted so the Application layer can
/// request a token without depending on the concrete JWT library.
/// </summary>
public interface ITokenProvider
{
    /// <summary>Generates a signed access token for an authenticated user.</summary>
    Result<TokenResponseDto> GenerateAccessToken(AppUserDto user);
}
