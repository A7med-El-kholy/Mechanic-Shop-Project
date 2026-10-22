using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Application.Features.WorkOrders.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Work order flow: open a job (vehicle + catalog services), evolve its state per
/// the lifecycle, edit services while editable, delete only before work starts,
/// and the three guards: archived vehicles reject bookings, vehicles with active
/// jobs reject deletion (history auto-archives), catalog items in use reject removal.
/// </summary>
[Collection("ApiDb")]
public sealed class WorkOrdersEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public WorkOrdersEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithVehicleAndTasks_ReturnsScheduledTotals()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId1 = await CreateTaskAsync(client, managerToken, $"Oil Change WO-{attempt}", 85m, 60);
        var taskId2 = await CreateTaskAsync(client, managerToken, $"Diag WO-{attempt}", 140m, 30);

        var createBody = new { vehicleId, serviceTaskIds = new[] { taskId1, taskId2 } };

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", createBody, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var workOrder = await ReadWorkOrder(created);
        workOrder!.State.Should().Be(0);
        workOrder.TotalPrice.Should().Be(225m);
        workOrder.EstimatedDurationTotalInMins.Should().Be(90);
        workOrder.ServiceTasks.Should().HaveCount(2);
        workOrder.VehicleId.Should().Be(vehicleId);
        workOrder.LicensePlate.Should().NotBeEmpty();
        workOrder.CustomerName.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_WithUnknownVehicle_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var taskId = await CreateTaskAsync(client, managerToken, $"Lone Task WO-{attempt}", 50m, 30);

        var createBody = new { vehicleId = Guid.NewGuid(), serviceTaskIds = new[] { taskId } };

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", createBody, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StatusLifecycle_ScheduledToCompleted_ThenDeleteBlocked()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"Lifecycle Task {attempt}", 95m, 45);

        var created = (await ReadWorkOrder(
            await SendAsync(
                client,
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId } },
                managerToken)))!;

        using var start = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{created.WorkOrderId}/status",
            new { state = 1 }, managerToken);

        start.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var complete = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{created.WorkOrderId}/status",
            new { state = 2 }, managerToken);

        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders/{created.WorkOrderId}", null, managerToken);

        var finished = await ReadWorkOrder(fetched);
        finished!.State.Should().Be(2);

        // History is frozen: no re-editing, no deletion.
        using var tasksBlocked = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{created.WorkOrderId}/tasks",
            new { serviceTaskIds = new[] { taskId } }, managerToken);

        tasksBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var deleteBlocked = await SendAsync(
            client, HttpMethod.Delete, $"/api/work-orders/{created.WorkOrderId}", null, managerToken);

        deleteBlocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StatusTransition_SkippingSteps_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"Skip Task {attempt}", 60m, 30);

        var created = (await ReadWorkOrder(
            await SendAsync(
                client,
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId } },
                managerToken)))!;

        using var skipped = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{created.WorkOrderId}/status",
            new { state = 2 }, managerToken);

        skipped.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ScheduledWorkOrder_RemovesIt()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"Delete Task {attempt}", 35m, 15);

        var created = (await ReadWorkOrder(
            await SendAsync(
                client,
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId } },
                managerToken)))!;

        using var removed = await SendAsync(
            client, HttpMethod.Delete, $"/api/work-orders/{created.WorkOrderId}", null, managerToken);

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders/{created.WorkOrderId}", null, managerToken);

        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveVehicle_WithActiveWorkOrder_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"Guard Task {attempt}", 45m, 30);

        await SendAsync(
            client,
            HttpMethod.Post,
            "/api/work-orders",
            new { vehicleId, serviceTaskIds = new[] { taskId } },
            managerToken);

        using var deleteAttempt = await SendAsync(
            client, HttpMethod.Delete, $"/api/vehicles/{vehicleId}", null, managerToken);

        deleteAttempt.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveVehicle_WithHistoryOnly_ArchivesAndRestores()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, customerId) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"History Task {attempt}", 45m, 30);

        var workOrder = (await ReadWorkOrder(
            await SendAsync(
                client,
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId } },
                managerToken)))!;

        // Cancel to clear the active state, leaving pure history.
        using var cancelled = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{workOrder.WorkOrderId}/status",
            new { state = 3 }, managerToken);

        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Delete with history -> auto-archive per prototype.
        using var removed = await SendAsync(
            client, HttpMethod.Delete, $"/api/vehicles/{vehicleId}", null, managerToken);

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var archived = await SendAsync(
            client, HttpMethod.Get, $"/api/vehicles/{vehicleId}", null, managerToken);

        var archivedVehicle = await archived.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        archivedVehicle.GetProperty("isArchived").GetBoolean().Should().BeTrue();

        // Archived vehicles leave customer lists.
        using var customerDetail = await SendAsync(
            client, HttpMethod.Get, $"/api/customers/{customerId}", null, managerToken);

        var customer = await customerDetail.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        customer.GetProperty("vehicles").GetArrayLength().Should().Be(0);

        // ...and reject new bookings.
        using var bookingAttempt = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/work-orders",
            new { vehicleId, serviceTaskIds = new[] { taskId } },
            managerToken);

        bookingAttempt.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Restore puts it back on the lists.
        using var restored = await SendAsync(
            client, HttpMethod.Put, $"/api/vehicles/{vehicleId}/restore", null, managerToken);

        restored.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var customerAfter = await SendAsync(
            client, HttpMethod.Get, $"/api/customers/{customerId}", null, managerToken);

        var restoredCustomer = await customerAfter.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        restoredCustomer.GetProperty("vehicles").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task List_FilterByState_ReturnsOnlyMatching()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"Filter Task {attempt}", 65m, 30);

        var scheduled = (await ReadWorkOrder(
            await SendAsync(
                client,
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId } },
                managerToken)))!;

        await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{scheduled.WorkOrderId}/status",
            new { state = 1 }, managerToken);

        using var listed = await SendAsync(
            client, HttpMethod.Get, "/api/work-orders?page=1&pageSize=100&state=1", null, managerToken);

        listed.StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        json.GetProperty("items")
            .EnumerateArray()
            .Should().OnlyContain(i => i.GetProperty("state").GetInt32() == 1);
    }

    [Fact]
    public async Task RemoveTask_ReferencedByWorkOrder_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, attempt);
        var taskId = await CreateTaskAsync(client, managerToken, $"InUse Task {attempt}", 45m, 30);

        await SendAsync(
            client,
            HttpMethod.Post,
            "/api/work-orders",
            new { vehicleId, serviceTaskIds = new[] { taskId } },
            managerToken);

        using var removed = await SendAsync(
            client, HttpMethod.Delete, $"/api/service-tasks/{taskId}", null, managerToken);

        removed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateWorkOrder_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/work-orders",
            new { vehicleId = Guid.NewGuid(), serviceTaskIds = new[] { Guid.NewGuid() } },
            token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<(Guid VehicleId, Guid CustomerId)> CreateCustomerWithVehicleAsync(
        HttpClient client,
        string managerToken,
        int attempt)
    {
        var body = new
        {
            name = $"WO Customer {attempt}",
            phoneNumber = $"+20120400{attempt:D3}",
            email = $"wo-cust-{attempt}@mechanicshop.test",
            vehicles = new[]
            {
                new
                {
                    make = "Mazda",
                    model = "3",
                    year = 2021,
                    licensePlate = $"WOV-{attempt:0000}",
                    vin = $"1HGCR2F59JA6{attempt:D5}"
                }
            }
        };

        using var response = await SendAsync(client, HttpMethod.Post, "/api/customers", body, managerToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (
            json.GetProperty("vehicles")[0].GetProperty("vehicleId").GetGuid(),
            json.GetProperty("customerId").GetGuid());
    }

    private static async Task<Guid> CreateTaskAsync(
        HttpClient client,
        string managerToken,
        string name,
        decimal price,
        int minutes)
    {
        var body = new { name, basePrice = price, estimatedDurationInMins = minutes };

        using var response = await SendAsync(client, HttpMethod.Post, "/api/service-tasks", body, managerToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return json.GetProperty("serviceTaskId").GetGuid();
    }

    private static async Task<WorkOrderDto?> ReadWorkOrder(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return await response.Content.ReadFromJsonAsync<WorkOrderDto>(
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
            // Plain StringContent instead of JsonContent: the MVC.Testing redirect
            // handler's content-cloning path walks the pipelined HttpContent object
            // graph (which trips over JsonContent's own Type-valued ObjectType
            // property). We serialize OUR object here — a value we know contains
            // only primitives — and ship raw UTF-8 bytes.
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
