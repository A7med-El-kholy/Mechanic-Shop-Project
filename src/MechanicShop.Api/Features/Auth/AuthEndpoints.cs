using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Infrastructure.Identity;

namespace MechanicShop.Api.Features.Auth;

/// <summary>
/// Authentication endpoints. Registration currently always creates a Customer —
/// self-signup is the only public path; Managers and Technicians are provisioned by
/// the shop (that flow lands in a later slice).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .WithName("Me")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IIdentityService identityService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await identityService.RegisterAsync(
            request.Email,
            request.Password,
            request.FullName,
            AppRoles.Customer,
            cancellationToken);

        if (result.IsError)
        {
            return result.TopError.ToProblem(httpContext);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IIdentityService identityService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await identityService.AuthenticateAsync(
            request.Email,
            request.Password,
            cancellationToken);

        if (result.IsError)
        {
            return result.TopError.ToProblem(httpContext);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> MeAsync(
        IIdentityService identityService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Error.Unauthorized("Auth.NoUserClaim", "Token is missing a user identifier.")
                .ToProblem(httpContext);
        }

        var result = await identityService.GetUserByIdAsync(userId, cancellationToken);

        if (result.IsError)
        {
            return result.TopError.ToProblem(httpContext);
        }

        return Results.Ok(result.Value);
    }
}
