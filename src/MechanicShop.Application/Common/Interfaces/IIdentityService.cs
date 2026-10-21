using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the identity store (ASP.NET Core Identity in Infrastructure).
/// The Application layer depends only on this contract and on plain DTOs,
/// never on Identity types — so auth logic stays testable without a database.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates a user with the given password and assigns them to a role.</summary>
    Task<Result<AppUserDto>> RegisterAsync(
        string email,
        string password,
        string fullName,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies credentials. Returns a token payload on success, or a
    /// <see cref="ErrorKind.Unauthorized"/> error on failure. It deliberately does not
    /// reveal whether the email exists or the password was wrong.
    /// </summary>
    Task<Result<TokenResponseDto>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches a user by their identifier.</summary>
    Task<Result<AppUserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
}
