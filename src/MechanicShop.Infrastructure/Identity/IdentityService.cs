using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.Identity;

/// <summary>
/// Bridges ASP.NET Core Identity to the Application layer. All Identity types stay
/// behind this class, so the Application layer and its tests never see them.
/// </summary>
public sealed class IdentityService(
    UserManager<AppUser> userManager,
    ITokenProvider tokenProvider,
    AppDbContext dbContext,
    ILogger<IdentityService> logger) : IIdentityService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly ITokenProvider _tokenProvider = tokenProvider;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ILogger<IdentityService> _logger = logger;

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

    public async Task<Result<AppUserDto>> RegisterCustomerAsync(
        string email,
        string password,
        string fullName,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var name = fullName.Trim();

        // Fail before creating anything when the identity account already exists.
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            return Error.Validation(
                "Identity.EmailAlreadyRegistered",
                "An account with this email already exists. Sign in instead.");
        }

        // A workshop-created profile may already exist for this email. If it is
        // unclaimed, signing up links it instead of creating a second profile.
        var existingCustomer = await _dbContext.Customers
            .FirstOrDefaultAsync(
                c => c.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (existingCustomer?.UserId is not null)
        {
            return Error.Validation(
                "Customer.AccountAlreadyLinked",
                "A customer account is already linked to this email. Sign in instead.");
        }

        Customer? newCustomer = null;

        if (existingCustomer is null)
        {
            var createResult = Customer.Create(
                Guid.NewGuid(),
                name,
                phoneNumber.Trim(),
                normalizedEmail,
                []);

            if (createResult.IsError)
            {
                return createResult.Errors;
            }

            newCustomer = createResult.Value;
        }

        // Identity's UserManager and this DbContext share the same scoped database
        // context, so one SQL transaction covers the account, the role assignment
        // and the customer profile. No half-registered accounts.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var userResult = await RegisterAsync(
                normalizedEmail,
                password,
                name,
                AppRoles.Customer,
                cancellationToken);

            if (userResult.IsError)
            {
                await transaction.RollbackAsync(cancellationToken);
                return userResult.Errors;
            }

            var customer = existingCustomer ?? newCustomer!;

            if (existingCustomer is null)
            {
                _dbContext.Customers.Add(customer);
            }

            var linkResult = customer.LinkUserAccount(userResult.Value.Id);

            if (linkResult.IsError)
            {
                await transaction.RollbackAsync(cancellationToken);
                return linkResult.Errors;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Customer account {UserId} registered and linked to customer {CustomerId}.",
                userResult.Value.Id,
                customer.Id);

            return userResult.Value;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogWarning(exception, "Customer registration failed for {Email}.", normalizedEmail);

            return Error.Validation(
                "Identity.RegisterFailed",
                "Registration could not be completed. The email may already be in use.");
        }
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

    public async Task<Result<Updated>> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Error.NotFound("Identity.UserNotFound", "The signed-in account no longer exists.");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (!result.Succeeded)
        {
            var description = string.Join(" ", result.Errors.Select(e => e.Description));

            return Error.Validation("Identity.ChangePasswordFailed", description);
        }

        return Result.Updated;
    }

    private async Task<AppUserDto> BuildDtoAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new AppUserDto(user.Id, user.Email!, user.FullName, roles.ToList());
    }
}
