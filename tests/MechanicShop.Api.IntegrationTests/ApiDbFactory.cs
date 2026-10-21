using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Testcontainers.MsSql;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Boots the real API host in-process AND a throwaway SQL Server in a container,
/// then points the app's "ConnectionStrings:MechanicShop" at it. Schema is created
/// by the app's own migrations and roles are seeded by <c>SeedData</c>, so every
/// test starts from the same real, migrated database.
///
/// One container is shared by all tests in the run (an xunit fixture), because
/// starting SQL Server per test is slow. Tests must not depend on each other's rows.
/// </summary>
public sealed class ApiDbFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async ValueTask InitializeAsync()
    {
        // Start the container first so ConfigureWebHost can read its connection string.
        // Schema and role seeding happen in Program.cs via SeedData.EnsureRolesAsync,
        // which migrates before it seeds.
        await _container.StartAsync();

        // Touching Services boots the host, which applies migrations and seeds roles.
        _ = Services;
    }

    public new async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();

        base.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:MechanicShop", _container.GetConnectionString());
    }
}
