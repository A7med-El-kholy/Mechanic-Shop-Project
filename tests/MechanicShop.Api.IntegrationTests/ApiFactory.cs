using Microsoft.AspNetCore.Mvc.Testing;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Boots the real API host in-process (no network, no separate process).
/// Host-only configuration: this factory does NOT provide a database.
/// Database-backed integration tests will compose Testcontainers on top
/// of this factory when the first persistence feature lands.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
}
