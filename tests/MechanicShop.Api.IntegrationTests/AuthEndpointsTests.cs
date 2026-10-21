using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Real end-to-end auth flow against a throwaway SQL Server container:
/// register → login → use the JWT on an authorized endpoint, plus the failure modes.
/// Each test uses its own email so tests do not depend on each other's rows.
/// </summary>
[Collection("ApiDb")]
public sealed class AuthEndpointsTests
{
    private readonly ApiDbFactory _factory;

    public AuthEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidInput_ReturnsCustomer()
    {
        using var client = _factory.CreateClient();

        var request = new RegisterRequest(
            "sarah@example.com",
            "Passw0rd!",
            "Sarah Peter");

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<AppUserDto>(
            TestContext.Current.CancellationToken);

        user!.Email.Should().Be("sarah@example.com");
        user.FullName.Should().Be("Sarah Peter");
        user.Roles.Should().ContainSingle().Which.Should().Be("Customer");
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var request = new RegisterRequest(
            "weak@example.com",
            "short",
            "Weak Password");

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var request = new RegisterRequest(
            "dupe@example.com",
            "Passw0rd!",
            "Dupe One");

        using var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            request,
            TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            request,
            TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsBearerToken()
    {
        using var client = _factory.CreateClient();

        var email = "login@example.com";

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Login User"),
            TestContext.Current.CancellationToken);

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Passw0rd!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>(
            TestContext.Current.CancellationToken);

        token!.TokenType.Should().Be("Bearer");
        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.ExpiresInSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var email = "wrongpw@example.com";

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Wrong Password"),
            TestContext.Current.CancellationToken);

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "NotThePassword1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/auth/me",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        using var client = _factory.CreateClient();

        var email = "me@example.com";

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Me User"),
            TestContext.Current.CancellationToken);

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Passw0rd!"),
            TestContext.Current.CancellationToken);

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await login.Content.ReadFromJsonAsync<TokenResponseDto>(
            TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/me");

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            token!.AccessToken);

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<AppUserDto>(
            TestContext.Current.CancellationToken);

        user!.Email.Should().Be(email);
        user.Roles.Should().ContainSingle().Which.Should().Be("Customer");
    }
}
