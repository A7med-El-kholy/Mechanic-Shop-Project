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
    /// Registers a self-service customer account AND its customer profile in one
    /// transaction. Staff-created customers already exist; this is the public path.
    /// </summary>
    Task<Result<AppUserDto>> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        string phoneNumber,
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

    /// <summary>
    /// Resolves the identity user id for an email, or null when no user has it.
    /// Used to link a customer profile to the account registered under the same email.
    /// </summary>
    Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Changes the signed-in user's password after verifying the current one.</summary>
    Task<Result<Updated>> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an existing account by email, or creates a new Customer account for a Google
    /// sign-in, and links the Google external login (providerKey) so the user can return
    /// with Google on later visits. Never creates duplicate accounts for the same email.
    /// </summary>
    Task<Result<AppUserDto>> FindOrCreateByGoogleAsync(
        string email,
        string? fullName,
        string providerKey,
        string providerDisplayName,
        CancellationToken cancellationToken = default);
}
