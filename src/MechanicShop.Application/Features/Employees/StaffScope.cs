using MechanicShop.Application.Common.Interfaces;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Employees.Enums;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Employees;

/// <summary>
/// Who the caller answers for on the shop floor. A staff account resolves through
/// its roster link, never through what a client passes in: a technician is exactly
/// one row of hands (<see cref="TechnicianId"/> set), a manager answers for the
/// whole workshop (no narrowing). An identity-technician with no roster row is a
/// provisioning defect — fail loudly rather than leak the shop behind a "my" screen.
/// </summary>
public sealed class TechnicianScope
{
    private TechnicianScope(Guid? technicianId)
    {
        TechnicianId = technicianId;
    }

    /// <summary>The caller's own roster row, or null when nothing narrows.</summary>
    public Guid? TechnicianId { get; }

    /// <summary>No narrowing — the caller answers for the whole workshop.</summary>
    public static readonly TechnicianScope Unscoped = new((Guid?)null);

    public static TechnicianScope Mine(Guid technicianId) => new(technicianId);
}

public static class StaffScope
{
    // Role names live in Infrastructure (AppRoles), which Application cannot
    // reference. The literals stay beside their only consumer; the authorization
    // tests catch a drift where the compiler cannot.
    private const string ManagerRoleName = "Manager";

    public static Error TechnicianNotLinked =>
        Error.Forbidden(
            "Employee_TechnicianNotLinked",
            "Technician account has no roster link. Ask a manager to re-provision the staff account.");

    public static async Task<Result<TechnicianScope>> ResolveMineAsync(
        IAppDbContext context,
        IUser caller,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caller.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        var employee = await context.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.UserId == caller.UserId, cancellationToken);

        if (employee is not null)
        {
            return employee.Role == EmployeeRole.Technician
                ? TechnicianScope.Mine(employee.Id)
                : TechnicianScope.Unscoped;
        }

        // The roster is the only source of truth for whose hands an account uses.
        // A manager login works without a link (the bootstrap manager predates the
        // link); every other unresolved account — identity-technicians without a
        // roster row, anything that somehow got past the endpoint policy — fails
        // loudly instead of seeing the shop as "everybody's".
        if (caller.Roles.Contains(ManagerRoleName))
        {
            return TechnicianScope.Unscoped;
        }

        return TechnicianNotLinked;
    }
}
