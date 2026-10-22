using System.Security.Claims;

using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Api.Common;

/// <summary>
/// Resolves the caller's identity from the HttpContext claims. "sub" is intact by
/// design (MapInboundClaims=false), so the Application layer sees the same id the
/// TokenProvider wrote into the token.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

    public IReadOnlyList<string> Roles =>
        _httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList()
            ?? [];
}
