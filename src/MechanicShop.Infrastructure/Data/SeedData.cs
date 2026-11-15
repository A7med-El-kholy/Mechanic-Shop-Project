using MechanicShop.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.Data;

/// <summary>
/// Idempotent startup seeding: applies migrations, then the three shop roles, then an
/// optional bootstrap Manager whose credentials come from configuration (user-secrets
/// locally, environment variables when deployed). Nothing is hard-coded in source.
/// Seeding only inserts what is missing, so it is safe to run on every start.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        // Migrate BEFORE seeding: the AspNetRoles *table* must exist before rows go in.
        // Doing it here keeps the ordering invariant in one place so no caller can get it wrong.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        // SQL Server Express turns AUTO_CLOSE on for new databases. That unloads
        // the database whenever the last connection closes, so the next request
        // pays a 15–20s recovery cost (and can time out). Keep it online.
        await db.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET AUTO_CLOSE OFF;");

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        await EnsureRolesAsync(scope, logger);
        await EnsureDefaultManagerAsync(scope, logger);
    }

    private static async Task EnsureRolesAsync(IServiceScope scope, ILogger logger)
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));

            if (result.Succeeded)
            {
                logger.LogInformation("Seeded role {Role}", role);
            }
            else
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));

                throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
            }
        }
    }

    private static async Task EnsureDefaultManagerAsync(IServiceScope scope, ILogger logger)
    {
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = configuration["Seed:ManagerEmail"];
        var password = configuration["Seed:ManagerPassword"];

        // Not configured = no bootstrap account; deployment decides intentionally.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return; // already provisioned — stay idempotent on re-runs
        }

        var manager = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = "Shop Manager",
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(manager, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException($"Failed to seed the bootstrap manager: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(manager, AppRoles.Manager);

        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException($"Failed to give the bootstrap manager its role: {errors}");
        }

        logger.LogInformation("Seeded bootstrap manager {Email}", email);
    }
}
