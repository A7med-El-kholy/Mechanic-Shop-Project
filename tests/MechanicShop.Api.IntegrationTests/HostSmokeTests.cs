using System.Net;
using AwesomeAssertions;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Host-only smoke test (no database involved): proves the API host builds,
/// the dependency-injection graph resolves, and routing + middleware execute
/// a real request end to end. Business features get their own tests later.
/// </summary>
public sealed class HostSmokeTests
{
    [Fact]
    public async Task GetRoot_ReturnsHelloWorld()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        body.Should().Be("Hello World!");
    }
}
