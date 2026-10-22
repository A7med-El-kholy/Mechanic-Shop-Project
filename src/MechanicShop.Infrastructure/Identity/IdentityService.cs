using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.Identity;

/// <summary>
/// Bridges ASP.NET Core Identity to the Application layer. All Identity types stay
/// behind this class, so the Application layer and its tests never see them.
/// </summary>
public sealed class IdentityService(
    UserManager<AppUser> userManager,
    ITokenProvider tokenProvider) : IIdentityService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly ITokenProvider _tokenProvider = tokenProvider;

    public async Task<Result<AppUserDto>> RegisterAsync(
        string email,
        string password,
        string fullName,
        string role,
        CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true, // no real email provider in this milestone; revisit with password reset
        };

        var createResult = await _userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            // Collapse Identity's granular errors into one validation error carrying every message.
            var description = string.Join(" ", createResult.Errors.Select(e => e.Description));

            return Error.Validation("Identity.RegisterFailed", description);
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, role);

        if (!addToRoleResult.Succeeded)
        {
            // The account exists but could not be put in its role — surface honestly.
            var description = string.Join(" ", addToRoleResult.Errors.Select(e => e.Description));

            return Error.Validation("Identity.RoleAssignmentFailed", description);
        }

        return await BuildDtoAsync(user);
    }

    public async Task<Result<TokenResponseDto>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        // One generic message for "no such user" and "wrong password" so callers cannot
        // probe which emails are registered.
        var invalidCredentials = Error.Unauthorized(
            "Identity.InvalidCredentials",
            "The email or password is incorrect.");

        if (user is null)
        {
            return invalidCredentials;
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            return invalidCredentials;
        }

        var userDto = await BuildDtoAsync(user);

        var tokenResult = _tokenProvider.GenerateAccessToken(userDto);

        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        return tokenResult.Value;
    }

    public async Task<Result<AppUserDto>> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Error.NotFound("Identity.UserNotFound", $"No user with id '{userId}'.");
        }

        return await BuildDtoAsync(user);
    }

    public async Task<string?> FindUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        return user?.Id;
    }

    private async Task<AppUserDto> BuildDtoAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new AppUserDto(user.Id, user.Email!, user.FullName, roles.ToList());
    }
}
