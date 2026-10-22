namespace MechanicShop.Application.Common.Interfaces;

/// <summary>
/// Identity-of-the-caller contract for the Application layer. Implemented in the Api
/// layer from the HttpContext claims ("sub"), so handlers can resolve the caller's
/// resources without depending on ASP.NET Core types.
/// </summary>
public interface IUser
{
    /// <summary>The authenticated user's id ("sub" claim), or null when anonymous.</summary>
    string? UserId { get; }

    IReadOnlyList<string> Roles { get; }
}
