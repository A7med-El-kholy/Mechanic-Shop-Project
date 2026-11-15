using System.Net;

using AwesomeAssertions;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Host smoke test against the shared throwaway SQL Server container: proves the
/// API host builds, the dependency-injection graph resolves, migrations + seeding
/// run, and routing + middleware execute a real request end to end.
/// </summary>
[Collection("ApiDb")]
public sealed class HostSmokeTests
{
    private readonly ApiDbFactory _factory;

    public HostSmokeTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_ReturnsHelloWorld()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        body.Should().Be("Hello World!");
    }
}
