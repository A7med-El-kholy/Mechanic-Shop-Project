using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Infrastructure.Identity;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

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
            .RequireRateLimiting("auth")
            .WithName("Register")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("auth")
            .WithName("Login")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .WithName("Me")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // --- Sign-in with Google.
        // /api/auth/google        -> starts the Google OAuth2 flow (redirects to Google).
        // /api/auth/google-callback-complete -> Google calls back here; we exchange the
        // external login for a JWT and redirect back to the SPA with the token in the URL
        // fragment (not sent to the server, short-lived).
        // /api/auth/google-failure -> surfaces OAuth failures cleanly.
        group.MapGet("/google", GoogleChallengeAsync)
            .WithName("GoogleChallenge")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/google-callback-complete", GoogleCallbackCompleteAsync)
            .WithName("GoogleCallbackComplete")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/google-failure", GoogleFailureAsync)
            .WithName("GoogleFailure")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IIdentityService identityService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await identityService.RegisterCustomerAsync(
            request.Email,
            request.Password,
            request.FullName,
            request.PhoneNumber,
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

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
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

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Error.Validation("Auth.PasswordRequired", "Both the current and new password are required.")
                .ToProblem(httpContext);
        }

        var result = await identityService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> GoogleChallengeAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(configuration["GoogleAuth:ClientId"]) ||
            string.IsNullOrWhiteSpace(configuration["GoogleAuth:ClientSecret"]))
        {
            return Error.Validation(
                    "Auth.GoogleNotConfigured",
                    "Sign-in with Google is not configured on the server. Set GoogleAuth:ClientId and GoogleAuth:ClientSecret.")
                .ToProblem(httpContext);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/api/auth/google-callback-complete?returnUrl={Uri.EscapeDataString(returnUrl ?? "/portal")}",
        };
        properties.Items["returnUrl"] = returnUrl ?? "/portal";

        // Results.Challenge invokes the Google handler once, which redirects the browser to Google.
        return Results.Challenge(properties, new[] { "Google" });
    }

    private static async Task<IResult> GoogleCallbackCompleteAsync(
        HttpContext httpContext,
        IIdentityService identityService,
        ITokenProvider tokenProvider,
        IConfiguration configuration,
        string? returnUrl)
    {
        var result = await httpContext.AuthenticateAsync("External");

        if (!result.Succeeded || result.Principal is null)
        {
            return Error.Unauthorized("Auth.GoogleFailed", "Google sign-in failed. Try again.")
                .ToProblem(httpContext);
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);
        var googleSubject = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(googleSubject))
        {
            return Error.Unauthorized("Auth.GoogleIncomplete", "Google did not return a usable account.")
                .ToProblem(httpContext);
        }

        var dto = await identityService.FindOrCreateByGoogleAsync(
            email,
            name,
            googleSubject,
            "Google",
            httpContext.RequestAborted);

        if (dto.IsError)
        {
            return dto.TopError.ToProblem(httpContext);
        }

        var tokenResult = tokenProvider.GenerateAccessToken(dto.Value);
        if (tokenResult.IsError)
        {
            return tokenResult.TopError.ToProblem(httpContext);
        }

        // The short-lived external cookie has served its purpose; remove it.
        await httpContext.SignOutAsync("External");

        var frontendBase = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
        var safeReturn = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/")
            ? "/portal"
            : returnUrl; // guard against open redirects (relative paths only)

        var token = tokenResult.Value.AccessToken;
        var expiresIn = tokenResult.Value.ExpiresInSeconds;
        var url =
            $"{frontendBase}/auth/callback#access_token={Uri.EscapeDataString(token)}&expires_in={expiresIn}&returnUrl={Uri.EscapeDataString(safeReturn)}";

        return Results.Redirect(url);
    }

    private static IResult GoogleFailureAsync(HttpContext httpContext, string? error)
    {
        return Error.Unauthorized(
                "Auth.GoogleFailed",
                $"Google sign-in failed: {error ?? "unknown"}")
            .ToProblem(httpContext);
    }
}
