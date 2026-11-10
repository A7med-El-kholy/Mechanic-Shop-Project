namespace MechanicShop.Api.Features.Users.Contracts;

/// <summary>
/// Body for POST /api/users/staff. Role must be Manager or Technician. An optional
/// EmployeeId binds the account to an existing roster row; without it, provisioning
/// creates a roster entry beside the login so a technician account always answers
/// for hands.
/// </summary>
public sealed record CreateStaffRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    Guid? EmployeeId = null);

/// <summary>The provisioned account plus the roster row it answers for.</summary>
public sealed record CreateStaffResponse(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    Guid? EmployeeId);
