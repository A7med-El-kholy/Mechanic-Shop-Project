using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Users.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Infrastructure.Identity;

namespace MechanicShop.Api.Features.Users;

/// <summary>
/// Manager-only staff provisioning. Public /api/auth/register stays Customer-only;
/// the role whitelist below is checked server-side, so a client can never ask for
/// a role it is not allowed to hand out.
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
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!ProvisionableRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
        {
            var allowed = string.Join("' or '", ProvisionableRoles);

            return Error.Validation("Users.InvalidRole", $"Role must be '{allowed}'.")
                .ToProblem(httpContext);
        }

        var result = await identityService.RegisterAsync(
            request.Email,
            request.Password,
            request.FullName,
            request.Role,
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }
}
