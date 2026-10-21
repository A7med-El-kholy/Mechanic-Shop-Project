using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.Identity;

/// <summary>
/// The application's user record, extending ASP.NET Core Identity's IdentityUser
/// (which already carries Id, Email, PasswordHash, PhoneNumber, etc.).
/// Lives in Infrastructure so the Domain layer stays free of Identity dependencies.
/// </summary>
public sealed class AppUser : IdentityUser
{
    /// <summary>Display name shown across the shop UI (e.g. "Sarah Peter").</summary>
    public string FullName { get; set; } = string.Empty;
}
