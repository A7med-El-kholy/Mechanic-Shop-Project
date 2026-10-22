using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Application.Features.ServiceTasks.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Staff catalog CRUD: create with validation, global name uniqueness (handler 409
/// plus DB race), paged/searchable listing, update and remove, and the auth matrix.
/// </summary>
[Collection("ApiDb")]
public sealed class ServiceTasksEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public ServiceTasksEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_StaffToken_ReturnsCreatedBody()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var createBody = new CreateTaskBody($"Cabin Air Filter Swap {attempt}", 220m, 45);

        using var created = await SendAsync(client, HttpMethod.Post, "/api/service-tasks", createBody, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var task = await ReadTask(created);
        task!.Name.Should().Be($"Cabin Air Filter Swap {attempt}");
        task.BasePrice.Should().Be(220m);
        task.EstimatedDurationInMins.Should().Be(45);
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var name = $"Suspension Check {attempt}";

        using var first = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody(name, 100m, 30),
            managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody(name, 110m, 60),
            managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(37)]
    [InlineData(195)]
    public async Task Create_OffGridDuration_ReturnsBadRequest(int minutes)
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody($"Duration Probe {attempt}", 90m, minutes),
            managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_SearchByName_FindsCreatedTask()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var name = $"Water Pump Swap {attempt}";

        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody(name, 410m, 90),
            managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var listed = await SendAsync(
            client,
            HttpMethod.Get,
            "/api/service-tasks?page=1&pageSize=100&search=water%20pump",
            null,
            managerToken);

        listed.StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        json.GetProperty("items")
            .EnumerateArray()
            .Any(i => i.GetProperty("name").GetString() == name)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/service-tasks/{Guid.NewGuid()}",
            null,
            managerToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_StaffToken_ChangesPriceAndDuration()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var name = $"Radiator Flush {attempt}";

        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody(name, 130m, 45),
            managerToken);

        var task = await ReadTask(created);

        using var updated = await SendAsync(
            client,
            HttpMethod.Put,
            $"/api/service-tasks/{task!.ServiceTaskId}",
            new UpdateTaskBody(name, 149.50m, 60),
            managerToken);

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/service-tasks/{task.ServiceTaskId}",
            null,
            managerToken);

        var refreshed = await ReadTask(fetched);
        refreshed!.BasePrice.Should().Be(149.50m);
        refreshed.EstimatedDurationInMins.Should().Be(60);
    }

    [Fact]
    public async Task Remove_StaffToken_DeletesTask()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody($"Bulb Replacement {attempt}", 35m, 15),
            managerToken);

        var task = await ReadTask(created);

        using var removed = await SendAsync(
            client,
            HttpMethod.Delete,
            $"/api/service-tasks/{task!.ServiceTaskId}",
            null,
            managerToken);

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/service-tasks/{task.ServiceTaskId}",
            null,
            managerToken);

        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody("Anon Probe", 10m, 30),
            token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_CustomerRole_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        var email = $"svc-no-role-{_sequence}@mechanicshop.test";
        _sequence++;

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "SVC No Role"),
            TestContext.Current.CancellationToken);

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/service-tasks",
            new CreateTaskBody("Customer Probe", 10m, 30),
            customerToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record CreateTaskBody(string Name, decimal BasePrice, int EstimatedDurationInMins);

    private sealed record UpdateTaskBody(string Name, decimal BasePrice, int EstimatedDurationInMins);

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
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<ServiceTaskDto?> ReadTask(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return await response.Content.ReadFromJsonAsync<ServiceTaskDto>(
            options,
            TestContext.Current.CancellationToken);
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
