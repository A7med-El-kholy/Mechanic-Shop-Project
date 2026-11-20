using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Customers.Contracts;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Customer registration creates and links the caller's profile immediately.
/// </summary>
[Collection("ApiDb")]
public sealed class MyCustomerFlowTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public MyCustomerFlowTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_AfterRegistration_ReturnsOwnEmptyProfile()
    {
        using var client = _factory.CreateClient();

        var email = $"me-first-{_sequence}@mechanicshop.test";
        _sequence++;

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Me First", "01000000000"),
            TestContext.Current.CancellationToken);

        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<CustomerDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, TestContext.Current.CancellationToken);
        profile!.Email.Should().Be(email);
        profile.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Me_AfterWorkshopCreatesProfileWithSameEmail_ReturnsOwnProfile()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var email = $"me-linked-{attempt}@mechanicshop.test";

        // The future customer registers themselves first — like the real flow.
        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Linked Customer", "01000000000"),
            TestContext.Current.CancellationToken);

        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        // Registration automatically creates and links an empty profile.
        using var before = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, customerToken),
            TestContext.Current.CancellationToken);

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownProfile = await before.Content.ReadFromJsonAsync<CustomerDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, TestContext.Current.CancellationToken);

        // Staff adds a vehicle to the already linked customer profile.
        var createBody = new CreateVehicleRequest(
            "Toyota", "Corolla", 2024, $"LNK-{attempt:0000}", $"1HGCR2F59JA4{attempt:d5}");

        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/customers/{ownProfile!.CustomerId}/vehicles", createBody, managerToken),
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        // Now "me" resolves from the token's user id — no path id involved.
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var me = await response.Content.ReadFromJsonAsync<CustomerDto>(options, TestContext.Current.CancellationToken);

        me!.Email.Should().Be(email);
        me.Vehicles.Should().ContainSingle().Which.LicensePlate.Should().Be($"LNK-{attempt:0000}");
    }

    [Fact]
    public async Task Me_ManagerToken_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Me_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/customers/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage RequestMessage(HttpMethod method, string uri, object? body, string token)
    {
        var request = new HttpRequestMessage(method, uri);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client, string email, string password)
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
