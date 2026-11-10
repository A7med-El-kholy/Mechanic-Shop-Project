using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Users.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Employees.Enums;
using MechanicShop.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Api.Features.Users;

/// <summary>
/// Manager-only staff provisioning. Public /api/auth/register stays Customer-only;
/// the role whitelist below is checked server-side, so a client can never ask for
/// a role it is not allowed to hand out.
///
/// A provisioned Technician always answers for hands: the call binds the login to
/// an existing (unlinked) roster row via EmployeeId, or creates the roster entry
/// beside the login. The identity commit happens first — if the roster save fails
/// the account remains but resolves to "no roster link", and the staff scoping wall
/// refuses it loudly rather than leaking the shop.
/// </summary>
public static class UsersEndpoints
{
    private static readonly string[] ProvisionableRoles = [AppRoles.Manager, AppRoles.Technician];

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users").WithTags("Users");

        group.MapPost("/staff", CreateStaffAsync)
            .WithName("CreateStaff")
            .RequireAuthorization(Policies.IsManager)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static async Task<IResult> CreateStaffAsync(
        CreateStaffRequest request,
        IIdentityService identityService,
        IAppDbContext context,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ProvisionableRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join("' or '", ProvisionableRoles);

            return Error.Validation("Users.InvalidRole", $"Role must be '{allowed}'.")
                .ToProblem(httpContext);
        }

        var employeeRole = string.Equals(request.Role, AppRoles.Manager, StringComparison.OrdinalIgnoreCase)
            ? EmployeeRole.Manager
            : EmployeeRole.Technician;

        Employee? linkedEmployee = null;

        if (request.EmployeeId is { } rosterId)
        {
            linkedEmployee = await context.Employees
                .FirstOrDefaultAsync(e => e.Id == rosterId, cancellationToken);

            if (linkedEmployee is null)
            {
                return Error.NotFound("Employee_NotFound", $"Employee '{rosterId}' was not found.")
                    .ToProblem(httpContext);
            }

            if (linkedEmployee.UserId is not null)
            {
                return Error.Conflict(
                    "Employee_AlreadyLinked",
                    $"Employee '{rosterId}' is already linked to a staff account.")
                    .ToProblem(httpContext);
            }

            if (linkedEmployee.Role != employeeRole)
            {
                return Error.Validation(
                    "Employee_RoleMismatch",
                    $"Staff account is '{request.Role}', but the roster entry is a '{linkedEmployee.Role}'. " +
                    "Provision the matching role.")
                    .ToProblem(httpContext);
            }
        }
        else
        {
            // The rosters FullName carries a unique index; catch the conflict before
            // the identity user exists, or a name clash would orphan an account.
            var fullName = request.FullName.Trim();

            var nameTaken = await context.Employees
                .AnyAsync(e => e.FullName == fullName, cancellationToken);

            if (nameTaken)
            {
                return Error.Conflict(
                    "Employee_Name_Taken",
                    $"An employee named '{fullName}' is already on the roster.")
                    .ToProblem(httpContext);
            }
        }

        var result = await identityService.RegisterAsync(
            request.Email,
            request.Password,
            request.FullName,
            request.Role,
            cancellationToken);

        if (result.IsError)
        {
            return result.TopError.ToProblem(httpContext);
        }

        var user = result.Value;

        var employee = linkedEmployee;

        if (employee is null)
        {
            var created = Employee.Create(Guid.NewGuid(), request.FullName, employeeRole);

            if (created.IsError)
            {
                return created.TopError.ToProblem(httpContext);
            }

            employee = created.Value;

            context.Employees.Add(employee);
        }

        var link = employee.LinkUserAccount(user.Id);

        if (link.IsError)
        {
            return link.TopError.ToProblem(httpContext);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new CreateStaffResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Roles,
            employee.Id));
    }
}
