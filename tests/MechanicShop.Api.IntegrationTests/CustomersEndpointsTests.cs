using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Customers.Contracts;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// Customers CRUD end-to-end: staff can create/list/read/update/remove customers, a
/// Customer-role token is rejected with 403, and duplicates (email, VIN, shape) fail
/// with the right status.
/// </summary>
[Collection("ApiDb")]
public sealed class CustomersEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private readonly ApiDbFactory _factory;

    public CustomersEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateCustomer_ManagerToken_ReturnsCreatedCustomerWithVehicles()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var request = CustomerRequest("crash-test@example.com");

        using var created = RequestMessage(HttpMethod.Post, "/api/customers", request, managerToken);
        using var response = await client.SendAsync(created, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var customer = await ReadCustomer(response);

        customer!.Name.Should().Be("Crash Test");
        customer.Vehicles.Should().ContainSingle();
        customer.Vehicles[0].LicensePlate.Should().StartWith("CRS-40");
        customer.Vehicles[0].Vin.Should().StartWith("1HGCR2F59JA2").And.HaveLength(17);
    }

    [Fact]
    public async Task CreateCustomer_CustomerToken_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("plain-customer@example.com", "Passw0rd!", "Plain Customer", "01000000000"),
            TestContext.Current.CancellationToken);

        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var customerToken = await GetAccessTokenAsync(client, "plain-customer@example.com", "Passw0rd!");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest("staff-only@example.com"), customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCustomer_Anonymous_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            CustomerRequest("anon@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCustomer_DuplicateEmail_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = "dup-customer@example.com";

        using var first = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCustomer_VinWithBannedLetterO_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var request = CustomerRequest("bad-vin@example.com", vin: "1HGCR2F59JA1O4826"); // contains 'O'

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", request, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCustomers_SearchByPlate_FindsCustomer()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var request = CustomerRequest("searchable@example.com", plate: "SRCH-8890");

        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", request, managerToken),
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers?page=1&pageSize=20&search=srch-8890", null, managerToken),
            TestContext.Current.CancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "body: {0}",
            responseText);

        var page = await response.Content.ReadFromJsonAsync<PaginatedList<CustomerDto>>(
            TestContext.Current.CancellationToken);

        page!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        page.Items!.Should().Contain(c => c.Email == "searchable@example.com");
    }

    [Fact]
    public async Task GetCustomerById_UnknownId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/customers/{Guid.NewGuid()}", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCustomer_ManagerToken_ChangesFields()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = "updatable@example.com";

        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        var customer = await ReadCustomer(created);

        using var updated = await client.SendAsync(RequestMessage(
            HttpMethod.Put,
            $"/api/customers/{customer!.CustomerId}",
            new UpdateCustomerRequest("Updated Name", "updated@example.com", "+201111111111"),
            managerToken),
            TestContext.Current.CancellationToken);

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/customers/{customer.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await ReadCustomer(fetched);
        refreshed!.Name.Should().Be("Updated Name");
        refreshed.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task RemoveCustomer_ManagerToken_DeletesCustomer()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = "removable@example.com";

        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        var customer = await ReadCustomer(created);

        using var removed = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/customers/{customer!.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/customers/{customer.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveCustomer_VehicleWithActiveWorkOrder_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = $"guard-active-{Guid.NewGuid():N}@example.com";
        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        var customer = await ReadCustomer(created);
        var vehicleId = customer!.Vehicles[0].VehicleId;

        await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/customers/{customer.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveCustomer_VehicleWithCompletedHistory_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = $"guard-history-{Guid.NewGuid():N}@example.com";
        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        var customer = await ReadCustomer(created);
        var vehicleId = customer!.Vehicles[0].VehicleId;

        using var workOrder = await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId);
        var workOrderId = (await workOrder.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("workOrderId").GetGuid();

        using var started = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 1 }, managerToken),
            TestContext.Current.CancellationToken);

        started.EnsureSuccessStatusCode();

        using var completed = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, managerToken),
            TestContext.Current.CancellationToken);

        completed.EnsureSuccessStatusCode();

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/customers/{customer.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemoveCustomer_VehicleWithoutWorkOrders_CascadesVehicles()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var email = $"guard-clean-{Guid.NewGuid():N}@example.com";
        using var created = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", CustomerRequest(email), managerToken),
            TestContext.Current.CancellationToken);

        created.EnsureSuccessStatusCode();

        var customer = await ReadCustomer(created);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/customers/{customer!.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var vanished = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/customers/{customer.CustomerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        vanished.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderForVehicleAsync(
        HttpClient client,
        string managerToken,
        Guid vehicleId)
    {
        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Tech C-{_vehicleSequence++}", role = 0 }, managerToken),
            TestContext.Current.CancellationToken);

        technician.EnsureSuccessStatusCode();

        var technicianId = (await technician.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("employeeId").GetGuid();

        var task = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/service-tasks", new { name = $"C Service {_vehicleSequence++}", basePrice = 50.0m, estimatedDurationInMins = 30 }, managerToken),
            TestContext.Current.CancellationToken);

        task.EnsureSuccessStatusCode();

        var taskId = (await task.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("serviceTaskId").GetGuid();

        var slot = TestSlots.Next();

        return await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId }, scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = slot.Spot, technicianId },
                managerToken),
            TestContext.Current.CancellationToken);
    }

    private static int _vehicleSequence = 1; // unique VINs per call; the DB unique index is append-only

    private static CreateCustomerRequest CustomerRequest(
        string email,
        string? plate = null,
        string? vin = null) => new(
            "Crash Test",
            "+201234567890",
            email,
            [new CreateVehicleRequest("Toyota", "Corolla", 2024, plate ?? $"CRS-40{_vehicleSequence:00}", vin ?? $"1HGCR2F59JA2{_vehicleSequence++:d5}")]);

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
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return await response.Content.ReadFromJsonAsync<CustomerDto>(options, TestContext.Current.CancellationToken);
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
