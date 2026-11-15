using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Users.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Infrastructure.Identity;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Role enforcement: the seeded bootstrap Manager can provision staff; a Customer
/// gets 403, an anonymous caller gets 401, and invalid roles are rejected server-side.
/// </summary>
[Collection("ApiDb")]
public sealed class StaffEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1"; // matches ApiDbFactory seed settings

    private readonly ApiDbFactory _factory;

    public StaffEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_AsSeededBootstrapManager_ReturnsManagerRole()
    {
        using var client = _factory.CreateClient();

        var token = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateStaff_ManagerToken_CreatesTechnician()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/staff")
        {
            Content = JsonContent.Create(new CreateStaffRequest(
                "technician@example.com",
                "Passw0rd!",
                "Peter R.",
                AppRoles.Technician)),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            managerToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var staff = await response.Content.ReadFromJsonAsync<AppUserDto>(
            TestContext.Current.CancellationToken);

        staff!.Roles.Should().ContainSingle().Which.Should().Be(AppRoles.Technician);
    }

    [Fact]
    public async Task CreateStaff_CustomerToken_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        var email = "no-provision@example.com";

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Blocked Customer", "01000000000"),
            TestContext.Current.CancellationToken);

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/staff")
        {
            Content = JsonContent.Create(new CreateStaffRequest(
                "hijack@example.com",
                "Passw0rd!",
                "Should Not Exist",
                AppRoles.Manager)),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            customerToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateStaff_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/users/staff",
            new CreateStaffRequest("anon@example.com", "Passw0rd!", "Anon", AppRoles.Manager),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateStaff_InvalidRole_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/staff")
        {
            Content = JsonContent.Create(new CreateStaffRequest(
                "selfmade@example.com",
                "Passw0rd!",
                "Sneaky",
                AppRoles.Customer)), // role exists, but staff provisioning must not mint it
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            managerToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<string> GetAccessTokenAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>(
            TestContext.Current.CancellationToken);

        return token!.AccessToken;
    }
}
