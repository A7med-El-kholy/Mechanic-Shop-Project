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
/// The workshop-first customer flow: a user signs up (Customer role), the workshop
/// creates their profile with the SAME email, and the system links them — after that
/// GET /api/customers/me resolves the caller's own profile from the token, never from
/// a client-supplied id.
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
    public async Task Me_BeforeProfileExists_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var email = $"me-first-{_sequence}@mechanicshop.test";
        _sequence++;

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Me First"),
            TestContext.Current.CancellationToken);

        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            new RegisterRequest(email, "Passw0rd!", "Linked Customer"),
            TestContext.Current.CancellationToken);

        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        // Sanity: profile not there yet.
        using var before = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers/me", null, customerToken),
            TestContext.Current.CancellationToken);

        before.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The workshop creates the profile with the SAME email.
        var createBody = new CreateCustomerRequest(
            "Linked Customer",
            "+201234567890",
            email,
            [new CreateVehicleRequest("Toyota", "Corolla", 2024, $"LNK-{attempt:0000}", $"1HGCR2F59JA4{attempt:d5}")]);

        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", createBody, managerToken),
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
