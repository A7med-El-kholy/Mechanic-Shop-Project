namespace MechanicShop.Api.Features.Auth.Contracts;

/// <summary>Body for POST /api/auth/change-password.</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
