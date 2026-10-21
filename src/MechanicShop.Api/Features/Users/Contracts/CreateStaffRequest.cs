namespace MechanicShop.Api.Features.Users.Contracts;

/// <summary>Body for POST /api/users/staff. Role must be Manager or Technician.</summary>
public sealed record CreateStaffRequest(
    string Email,
    string Password,
    string FullName,
    string Role);
