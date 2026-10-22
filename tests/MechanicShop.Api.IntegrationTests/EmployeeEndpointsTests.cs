using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Roster management: staff curates the technician list that scheduling cites.
/// Names are unique, roles are whitelisted, and roster entries with work-order
/// history cannot be removed.
/// </summary>
[Collection("ApiDb")]
public sealed class EmployeeEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public EmployeeEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithValidName_ReturnsTechnician()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/employees",
            new { fullName = $"Technician EP-{attempt}", role = 0 },
            managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var employee = await ReadEmployee(created);

        employee!.FullName.Should().Be($"Technician EP-{attempt}");
        employee.Role.Should().Be(0);
        employee.EmployeeId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var name = $"Technician DUP-{attempt}";

        using var first = await SendAsync(
            client, HttpMethod.Post, "/api/employees", new { fullName = name, role = 0 }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = await SendAsync(
            client, HttpMethod.Post, "/api/employees", new { fullName = $" {name} ", role = 0 }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithInvalidRole_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/employees",
            new { fullName = $"Technician RoleBad-{attempt}", role = 9 },
            managerToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsRoster_WithTechniciansFilter()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        await SendAsync(client, HttpMethod.Post, "/api/employees", new { fullName = $"Technician LIST-{attempt}", role = 0 }, managerToken);

        await SendAsync(client, HttpMethod.Post, "/api/employees", new { fullName = $"Manager LIST-{attempt}", role = 1 }, managerToken);

        using var roster = await SendAsync(client, HttpMethod.Get, "/api/employees", null, managerToken);

        roster.StatusCode.Should().Be(HttpStatusCode.OK);

        var rosterText = await roster.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var rosterJson = JsonSerializer.Deserialize<JsonElement>(rosterText);
        var rosterNames = rosterJson.EnumerateArray()
            .Select(e => e.GetProperty("fullName").GetString())
            .ToList();

        rosterNames.Should().Contain($"Technician LIST-{attempt}").And.Contain($"Manager LIST-{attempt}");

        using var technicians = await SendAsync(client, HttpMethod.Get, "/api/employees/technicians", null, managerToken);

        technicians.StatusCode.Should().Be(HttpStatusCode.OK);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var technicianList = await technicians.Content.ReadFromJsonAsync<List<EmployeeDto>>(
            options,
            TestContext.Current.CancellationToken);

        technicianList.Should().Contain(e => e.FullName == $"Technician LIST-{attempt}");
        technicianList.Should().NotContain(e => e.FullName == $"Manager LIST-{attempt}");
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var response = await SendAsync(
            client, HttpMethod.Delete, $"/api/employees/{Guid.NewGuid()}", null, managerToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/employees",
            new { fullName = "Technician Anon", role = 0 },
            token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<EmployeeDto?> ReadEmployee(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return await response.Content.ReadFromJsonAsync<EmployeeDto>(
            options,
            TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string uri,
        object? body,
        string? token)
    {
        var request = new HttpRequestMessage(method, uri);

        if (body is not null)
        {
            // Plain StringContent: the MVC.Testing content-cloning path mis-handles
            // JsonContent (see WorkOrdersEndpointsTests).
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                System.Text.Encoding.UTF8,
                "application/json");
        }

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

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
