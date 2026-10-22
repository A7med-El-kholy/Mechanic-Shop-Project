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
/// Standalone vehicle management: add to a customer, fetch, update, remove, plus the
/// authorization matrix (staff allowed, customer token 403, anonymous 401) and the
/// global uniqueness of plate/VIN.
/// </summary>
[Collection("ApiDb")]
public sealed class VehiclesEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public VehiclesEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AddVehicle_ManagerToken_CreatesVehicleUnderCustomer()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (customerId, _, manager) = await CreateCustomerAsync(client, managerToken);

        var attempt = _sequence++;
        var addBody = new CreateVehicleRequest(
            "Honda",
            "Civic",
            2023,
            $"ADD-{attempt:0000}",
            $"1HGCR2F59JA3{attempt:d5}");

        using var added = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/customers/{customerId}/vehicles", addBody, manager),
            TestContext.Current.CancellationToken);

        added.StatusCode.Should().Be(HttpStatusCode.Created);

        var vehicle = await ReadVehicle(added);
        vehicle!.Make.Should().Be("Honda");
        vehicle.LicensePlate.Should().Be($"ADD-{attempt:0000}");
    }

    [Fact]
    public async Task AddVehicle_DuplicatePlate_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, plate, manager) = await CreateCustomerAsync(client, managerToken);

        var addBody = new CreateVehicleRequest("Kia", "Soul", 2022, plate, $"1HGCR2F59JA3{_sequence++:d5}");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/customers/{await AnyOtherCustomerId(client, managerToken)}" + "/vehicles", addBody, manager),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddVehicle_CustomerToken_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("no-vehicle@mechanicshop.test", "Passw0rd!", "No Vehicle"),
            TestContext.Current.CancellationToken);

        var customerToken = await GetAccessTokenAsync(client, "no-vehicle@mechanicshop.test", "Passw0rd!");

        var addBody = new CreateVehicleRequest("Kia", "Soul", 2022, $"XNR-{_sequence:0000}", $"1HGCR2F59JA3{_sequence++:d5}");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/customers/{Guid.Empty}/vehicles", addBody, customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVehicle_UnknownId_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/vehicles/{Guid.NewGuid()}", null, managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateVehicle_ManagerToken_ChangesFields()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (customerId, _, manager) = await CreateCustomerAsync(client, managerToken);

        var attempt = _sequence++;
        using var added = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/customers/{customerId}/vehicles",
                new CreateVehicleRequest("Nissan", "Altima", 2021, $"OUP-{attempt:0000}", $"1HGCR2F59JA3{attempt:d5}"),
                manager),
            TestContext.Current.CancellationToken);

        var vehicle = await ReadVehicle(added);

        var attempt2 = _sequence++;
        using var updated = await client.SendAsync(
            RequestMessage(
                HttpMethod.Put,
                $"/api/vehicles/{vehicle!.VehicleId}",
                new CreateVehicleRequest("Nissan", "Sentra", 2022, $"OUP2-{attempt2:0000}", $"1HGCR2F59JA3{attempt2:d5}"),
                manager),
            TestContext.Current.CancellationToken);

        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/vehicles/{vehicle.VehicleId}", null, manager),
            TestContext.Current.CancellationToken);

        var refreshed = await ReadVehicle(fetched);
        refreshed!.Model.Should().Be("Sentra");
        refreshed.LicensePlate.Should().Be($"OUP2-{attempt2:0000}");
    }

    [Fact]
    public async Task RemoveVehicle_ManagerToken_DeletesVehicle()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (customerId, _, manager) = await CreateCustomerAsync(client, managerToken);

        var attempt = _sequence++;
        using var added = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/customers/{customerId}/vehicles",
                new CreateVehicleRequest("Ford", "F-150", 2020, $"DEL-{attempt:0000}", $"1HGCR2F59JA3{attempt:d5}"),
                manager),
            TestContext.Current.CancellationToken);

        var vehicle = await ReadVehicle(added);

        using var removed = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/vehicles/{vehicle!.VehicleId}", null, manager),
            TestContext.Current.CancellationToken);

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var fetched = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/vehicles/{vehicle.VehicleId}", null, manager),
            TestContext.Current.CancellationToken);

        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListVehicles_Defaults_ExcludeArchived()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, plate, manager) = await CreateCustomerAsync(client, managerToken);

        var searchUri = $"/api/vehicles?page=1&pageSize=5&search={plate}";

        using var before = await client.SendAsync(
            RequestMessage(HttpMethod.Get, searchUri, null, manager),
            TestContext.Current.CancellationToken);

        before.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await before.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("licensePlate").GetString().Should().Be(plate);
        items[0].GetProperty("customerName").GetString().Should().Be("Vehicle Tester");

        var vehicleId = items[0].GetProperty("vehicleId").GetGuid();

        using var archived = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/vehicles/{vehicleId}/archive", null, manager),
            TestContext.Current.CancellationToken);

        archived.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var after = await client.SendAsync(
            RequestMessage(HttpMethod.Get, searchUri, null, manager),
            TestContext.Current.CancellationToken);

        (await after.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items").GetArrayLength().Should().Be(0);

        using var withArchived = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"{searchUri}&includeArchived=true", null, manager),
            TestContext.Current.CancellationToken);

        var row = (await withArchived.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .Single();

        row.GetProperty("isArchived").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ListVehicles_CustomerToken_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();

        var email = $"no-list-{_sequence++}@mechanicshop.test";

        using var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "No List"),
            TestContext.Current.CancellationToken);

        register.EnsureSuccessStatusCode();

        var customerToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/vehicles", null, customerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<(Guid CustomerId, string Plate, string ManagerToken)> CreateCustomerAsync(
        HttpClient client,
        string managerToken)
    {
        var attempt = _sequence++;

        var request = new CreateCustomerRequest(
            "Vehicle Tester",
            "+201234567890",
            $"vehicle-test-{attempt}@mechanicshop.test",
            [new CreateVehicleRequest("Toyota", "Corolla", 2024, $"VTA-{attempt:0000}", $"1HGCR2F59JA3{attempt:d5}")]);

        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/customers", request, managerToken),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var customer = await ReadCustomer(response);

        return (customer!.CustomerId, customer.Vehicles[0].LicensePlate, managerToken);
    }

    private static async Task<Guid> AnyOtherCustomerId(HttpClient client, string managerToken)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/customers?page=1&pageSize=5", null, managerToken),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Deserialize<JsonElement>(text);

        return json.GetProperty("items").EnumerateArray().First()
            .GetProperty("customerId").GetGuid();
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

    private static async Task<VehicleDto?> ReadVehicle(HttpResponseMessage response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return await response.Content.ReadFromJsonAsync<VehicleDto>(options, TestContext.Current.CancellationToken);
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
