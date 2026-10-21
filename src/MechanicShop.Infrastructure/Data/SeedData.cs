using MechanicShop.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Infrastructure.Data;

/// <summary>
/// Idempotent startup seeding. Roles must exist before <c>AddToRoleAsync</c> is called —
/// the migration creates the AspNetRoles *table*, but the rows have to be inserted.
/// Seeding runs on every start and only inserts what is missing, so it is safe to re-run.
/// </summary>
public static class SeedData
{
    public static async Task EnsureRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        // Migrate first: the AspNetRoles *table* must exist before roles can be inserted.
        // Doing it here keeps the ordering invariant in one place so no caller can get it wrong.
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

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
}
