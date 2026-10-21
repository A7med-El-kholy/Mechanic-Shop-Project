namespace MechanicShop.Api.Features.Auth.Contracts;

/// <summary>Body for POST /api/auth/login.</summary>
public sealed record LoginRequest(
    string Email,
    string Password);
