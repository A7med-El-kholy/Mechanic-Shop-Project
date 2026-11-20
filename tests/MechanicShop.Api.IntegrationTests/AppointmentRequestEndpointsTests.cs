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
/// The customer portal's data wall. Requests resolve from the token (never from a
/// client-supplied id), only a linked profile can act, and the workshop rules for
/// deletion guards hold across the new entity: vehicles with pending requests can
/// not be deleted, service tasks cited on requests can not be retired, and staff
/// read requests while customers read only their own.
/// </summary>
[Collection("ApiDb")]
public sealed class AppointmentRequestEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public AppointmentRequestEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_MyVehicle_ReturnsCreated()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = (Guid?)null, preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var mine = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/appointment-requests/mine", null, customerToken),
            TestContext.Current.CancellationToken);

        mine.StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = (await mine.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .EnumerateArray()
            .ToList();

        rows.Should().ContainSingle();

        var row = rows[0];

        row.GetProperty("vehicleId").GetGuid().Should().Be(ownVehicleId);
        row.GetProperty("preferredDate").GetString().Should().Be("2027-05-01");
        row.GetProperty("status").GetInt32().Should().Be(0); // Pending
        row.GetProperty("serviceTaskName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Create_WithTriageNotesAndUrgency_PersistsForReview()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        const string report =
            "My front brakes grind when I stop.\nWhen does it happen? When braking\nAny dashboard warning lights? Yes";

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new
                {
                    vehicleId = ownVehicleId,
                    serviceTaskId = (Guid?)null,
                    preferredDate = "2027-05-01",
                    notes = report,
                    urgency = 2
                },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var mine = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/appointment-requests/mine", null, customerToken),
            TestContext.Current.CancellationToken);
        mine.EnsureSuccessStatusCode();

        var rows = (await mine.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .EnumerateArray()
            .ToList();

        var row = rows.First(r => r.GetProperty("vehicleId").GetGuid() == ownVehicleId);

        row.GetProperty("notes").GetString().Should().Be(report);
        row.GetProperty("urgency").GetInt32().Should().Be(2); // High, as reported
    }

    [Fact]
    public async Task Create_UnknownUrgency_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new
                {
                    vehicleId = ownVehicleId,
                    serviceTaskId = (Guid?)null,
                    preferredDate = "2027-05-01",
                    urgency = 99
                },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_OtherCustomersVehicle_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var (_, otherVehicleId, _) = await CreateLinkedCustomerAsync(client, managerToken);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = otherVehicleId, serviceTaskId = (Guid?)null, preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_PastPreferredDate_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = (Guid?)null, preferredDate = "2020-01-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_UnknownServiceTask_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = Guid.NewGuid(), preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StaffList_ShowsPending_AndCustomerForbidden()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var created = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = (Guid?)null, preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        using var staff = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/appointment-requests", null, managerToken),
            TestContext.Current.CancellationToken);

        staff.StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = (await staff.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).EnumerateArray().ToList();

        rows.Should().Contain(r => r.GetProperty("vehicleId").GetGuid() == ownVehicleId);

        using var asCustomer = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/appointment-requests", null, customerToken),
            TestContext.Current.CancellationToken);

        asCustomer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveServiceTask_CitedOnPendingRequest_ReturnsConflict()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        using var taskResponse = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/service-tasks",
                new { name = $"Portal Task {attempt}", basePrice = 75.0m, estimatedDurationInMins = 45 },
                managerToken),
            TestContext.Current.CancellationToken);

        taskResponse.EnsureSuccessStatusCode();

        var taskId = (await taskResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("serviceTaskId").GetGuid();

        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var created = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = (Guid?)taskId, preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        using var removed = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/service-tasks/{taskId}", null, managerToken),
            TestContext.Current.CancellationToken);

        removed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveVehicle_PendingRequest_ReturnsConflict()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (ownCustomerId, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var created = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/appointment-requests",
                new { vehicleId = ownVehicleId, serviceTaskId = (Guid?)null, preferredDate = "2027-05-01" },
                customerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        using var removed = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/vehicles/{ownVehicleId}", null, managerToken),
            TestContext.Current.CancellationToken);

        removed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MyRepairs_SeesOwnHistory_AndNotOthers()
    {
        using var client = _factory.CreateClient();

        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, ownVehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var (_, otherVehicleId, _) = await CreateLinkedCustomerAsync(client, managerToken);

        await CreateWorkOrderForVehicleAsync(client, managerToken, ownVehicleId);

        var otherWorkOrderResponse = await CreateWorkOrderForVehicleAsync(client, managerToken, otherVehicleId);

        var otherWorkOrderId = (await otherWorkOrderResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("workOrderId").GetGuid();

        using var mine = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/repairs?page=1&pageSize=10", null, customerToken),
            TestContext.Current.CancellationToken);

        mine.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await mine.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        page.GetProperty("items").EnumerateArray()
            .AsEnumerable()
            .Where(i => i.GetProperty("vehicleId").GetGuid() == ownVehicleId)
            .Should().HaveCount(1);

        using var ownDetail = await client.SendAsync(
            RequestMessage(
                HttpMethod.Get,
                $"/api/portal/repairs/{page.GetProperty("items").EnumerateArray().First().GetProperty("workOrderId").GetGuid()}",
                null,
                customerToken),
            TestContext.Current.CancellationToken);

        ownDetail.StatusCode.Should().Be(HttpStatusCode.OK);

        using var otherDetail = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/portal/repairs/{otherWorkOrderId}", null, customerToken),
            TestContext.Current.CancellationToken);

        otherDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- helpers ---------------------------------------------------------------

    private static async Task<(Guid CustomerId, Guid VehicleId, string Token)> CreateLinkedCustomerAsync(
        HttpClient client,
        string managerToken)
    {
        return await TestCustomerFactory.RegisterWithVehicleAsync(
            client, managerToken, "Portal Tester", "portal", "Honda", "Civic", "PRT", "1HGCR2F59JA5");
    }

    private static void EnsureOk(string step, HttpResponseMessage response, string? vin = null)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();

            throw new HttpRequestException(
                $"{step} (vin={vin ?? "n/a"}) -> {(int)response.StatusCode}: {body}");
        }
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderForVehicleAsync(
        HttpClient client,
        string managerToken,
        Guid vehicleId)
    {
        var attempt = _sequence++;

        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Portal Tech {attempt}", role = 0 }, managerToken),
            TestContext.Current.CancellationToken);

        technician.EnsureSuccessStatusCode();

        var technicianId = (await technician.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("employeeId").GetGuid();

        var task = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/service-tasks",
                new { name = $"Portal Service {attempt}", basePrice = 60.0m, estimatedDurationInMins = 30 },
                managerToken),
            TestContext.Current.CancellationToken);

        task.EnsureSuccessStatusCode();

        var taskId = (await task.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("serviceTaskId").GetGuid();

        var slot = TestSlots.Next();

        return await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId }, scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = slot.Spot, technicianId },
                managerToken),
            TestContext.Current.CancellationToken);
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

    private static async Task<CustomerDto?> ReadCustomer(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return JsonSerializer.Deserialize<CustomerDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<TokenResponseDto>(TestContext.Current.CancellationToken);

        return auth!.AccessToken;
    }
}
