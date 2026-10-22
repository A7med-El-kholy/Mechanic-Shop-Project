using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Application.Features.WorkOrders.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// The scheduling rules served over HTTP: bay/technician conflicts block a
/// booking with the prototype's message wording, adjacency is legal, finished
/// jobs stay out of the conflict scan, only Scheduled orders reschedule, and
/// roster entries with assignments can't be removed. Each test books from the
/// shared TestSlots book so unrelated bookings can't conflict by accident.
/// </summary>
[Collection("ApiDb")]
public sealed class WorkOrderSchedulingEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public WorkOrderSchedulingEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_Scheduled_ReturnsSchedulingFields()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var workOrder = await ReadWorkOrder(created);

        workOrder!.ScheduledDate.Should().Be(slot.Date);
        workOrder.StartsAtMinute.Should().Be(slot.Minute);
        workOrder.EndsAtMinute.Should().Be(slot.Minute + 30); // booking inputs create a 30-minute service
        workOrder.Spot.Should().Be(slot.Spot);
        workOrder.TechnicianName.Should().NotBeEmpty();
        workOrder.State.Should().Be(0);
    }

    [Fact]
    public async Task Create_SameBayOverlapping_ReturnsConflictWithPrototypeMessage()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var first = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        attempt = _sequence++;
        var (secondVehicleId, secondTaskId, secondTechnicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);

        using var second = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId = secondVehicleId,
            serviceTaskIds = new[] { secondTaskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute + 15, // inside the first job's 30-minute block
            spot = slot.Spot,                  // same bay, different technician
            technicianId = secondTechnicianId
        }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var text = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        text.Should().Contain($"Bay {BayName(slot.Spot)} is occupied")
            .And.Contain($"{Fmt(slot.Minute)}–{Fmt(slot.Minute + 30)}");
    }

    [Fact]
    public async Task Create_SameTechnicianOverlapping_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var first = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        attempt = _sequence++;
        var (secondVehicleId, secondTaskId, _) = await CreateBookingInputsAsync(client, managerToken, attempt);

        using var second = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId = secondVehicleId,
            serviceTaskIds = new[] { secondTaskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute + 15, // overlaps the first job in time
            spot = (slot.Spot + 1) % 3,        // different bay
            technicianId                       // the first job's technician
        }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var text = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        text.Should().Contain("is already booked")
            .And.Contain($"{Fmt(slot.Minute)}–{Fmt(slot.Minute + 30)}");
    }

    [Fact]
    public async Task Create_AdjacentBlocks_DontConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (firstSlot, secondSlot) = TestSlots.NextPair();

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);

        using var first = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = firstSlot.Date,
            startsAtMinute = firstSlot.Minute,
            spot = firstSlot.Spot,
            technicianId
        }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        attempt = _sequence++;
        var (secondVehicleId, secondTaskId, secondTechnicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);

        using var second = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId = secondVehicleId,
            serviceTaskIds = new[] { secondTaskId },
            scheduledDate = secondSlot.Date,
            startsAtMinute = secondSlot.Minute, // exactly where the first job ends
            spot = secondSlot.Spot,
            technicianId = secondTechnicianId
        }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_OverFinishedJob_IsAllowed()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var first = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstOrder = await ReadWorkOrder(first);

        using var completed = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{firstOrder!.WorkOrderId}/status",
            new { state = 1 }, managerToken);

        completed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var finished = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{firstOrder.WorkOrderId}/status",
            new { state = 2 }, managerToken);

        finished.StatusCode.Should().Be(HttpStatusCode.NoContent);

        attempt = _sequence++;
        var (secondVehicleId, secondTaskId, _) = await CreateBookingInputsAsync(client, managerToken, attempt);

        // Same day, block, bay and technician as the now-completed job.
        using var second = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId = secondVehicleId,
            serviceTaskIds = new[] { secondTaskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithUnknownTechnician_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, _) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId = Guid.NewGuid()
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithOffGridStart_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute + 7, // off the 15-minute grid
            spot = slot.Spot,
            technicianId
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reschedule_ToFreeSlot_UpdatesTheJob()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();
        var target = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var workOrder = await ReadWorkOrder(created);

        using var moved = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{workOrder!.WorkOrderId}/schedule",
            new { scheduledDate = target.Date, startsAtMinute = target.Minute, spot = target.Spot, technicianId },
            managerToken);

        moved.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders/{workOrder.WorkOrderId}", null, managerToken);

        var updated = await ReadWorkOrder(fetched);

        updated!.ScheduledDate.Should().Be(target.Date);
        updated.StartsAtMinute.Should().Be(target.Minute);
        updated.Spot.Should().Be(target.Spot);
    }

    [Fact]
    public async Task Reschedule_IntoOccupiedBay_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var firstSlot = TestSlots.Next();

        using var first = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = firstSlot.Date,
            startsAtMinute = firstSlot.Minute,
            spot = firstSlot.Spot,
            technicianId
        }, managerToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        attempt = _sequence++;
        var (secondVehicleId, secondTaskId, secondTechnicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var secondSlot = TestSlots.Next();

        using var second = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId = secondVehicleId,
            serviceTaskIds = new[] { secondTaskId },
            scheduledDate = secondSlot.Date,
            startsAtMinute = secondSlot.Minute,
            spot = secondSlot.Spot,
            technicianId = secondTechnicianId
        }, managerToken);

        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondOrder = await ReadWorkOrder(second);

        // Move the second job onto the first job's own block: same bay, overlapping,
        // different technician — a pure bay conflict.
        using var moved = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{secondOrder!.WorkOrderId}/schedule",
            new { scheduledDate = firstSlot.Date, startsAtMinute = firstSlot.Minute, spot = firstSlot.Spot, technicianId = secondTechnicianId },
            managerToken);

        moved.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var text = await moved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        text.Should().Contain($"Bay {BayName(firstSlot.Spot)} is occupied");
    }

    [Fact]
    public async Task Reschedule_AfterWorkStarted_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var workOrder = await ReadWorkOrder(created);

        using var started = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{workOrder!.WorkOrderId}/status",
            new { state = 1 }, managerToken);

        started.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Body is grid-valid on purpose: the conflict must come from the state
        // machine, not from validation.
        using var moved = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{workOrder.WorkOrderId}/schedule",
            new { scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = (slot.Spot + 1) % 3, technicianId },
            managerToken);

        moved.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveTechnician_AssignedToWorkOrder_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var (vehicleId, taskId, technicianId) = await CreateBookingInputsAsync(client, managerToken, attempt);
        var slot = TestSlots.Next();

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", new
        {
            vehicleId,
            serviceTaskIds = new[] { taskId },
            scheduledDate = slot.Date,
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        }, managerToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var removed = await SendAsync(
            client, HttpMethod.Delete, $"/api/employees/{technicianId}", null, managerToken);

        removed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static string Fmt(int minute) => $"{minute / 60:D2}:{minute % 60:D2}";

    private static string BayName(int spot) => "ABC"[spot].ToString();

    private static async Task<(Guid VehicleId, Guid TaskId, Guid TechnicianId)> CreateBookingInputsAsync(
        HttpClient client,
        string managerToken,
        int attempt)
    {
        var customerBody = new
        {
            name = $"SC Customer {attempt}",
            phoneNumber = $"+20123456{attempt:D4}",
            email = $"sc-cust-{attempt}@mechanicshop.test",
            vehicles = new[]
            {
                new
                {
                    make = "Kia",
                    model = "Sportage",
                    year = 2022,
                    licensePlate = $"SCP-{attempt:0000}",
                    vin = $"1HGCR2F59JA8{attempt:D5}"
                }
            }
        };

        using var customerResponse = await SendAsync(client, HttpMethod.Post, "/api/customers", customerBody, managerToken);

        customerResponse.EnsureSuccessStatusCode();

        var customerJson = await customerResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var vehicleId = customerJson.GetProperty("vehicles")[0].GetProperty("vehicleId").GetGuid();

        var taskResponse = await SendAsync(client, HttpMethod.Post, "/api/service-tasks", new
        {
            name = $"SC Service {attempt}",
            basePrice = 60m,
            estimatedDurationInMins = 30
        }, managerToken);

        taskResponse.EnsureSuccessStatusCode();

        var taskJson = await taskResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var taskId = taskJson.GetProperty("serviceTaskId").GetGuid();

        var technicianResponse = await SendAsync(client, HttpMethod.Post, "/api/employees", new
        {
            fullName = $"Technician SC-{attempt}",
            role = 0
        }, managerToken);

        technicianResponse.EnsureSuccessStatusCode();

        var technicianJson = await technicianResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var technicianId = technicianJson.GetProperty("employeeId").GetGuid();

        return (vehicleId, taskId, technicianId);
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
