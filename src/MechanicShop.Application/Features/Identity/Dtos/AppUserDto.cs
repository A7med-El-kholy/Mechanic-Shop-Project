namespace MechanicShop.Application.Features.Identity.Dtos;

/// <summary>Identity-shaped user info, free of any ASP.NET Identity types.</summary>
public sealed record AppUserDto(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);
