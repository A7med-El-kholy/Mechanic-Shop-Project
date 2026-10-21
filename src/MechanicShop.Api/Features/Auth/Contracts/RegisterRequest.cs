namespace MechanicShop.Api.Features.Auth.Contracts;

/// <summary>Body for POST /api/auth/register.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName);
