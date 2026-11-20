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
/// The shelf contract: the manager reads and moves stock with clear, derived
/// states (in / low / out from the minimum level), customers reserve shop orders
/// for cash-at-pickup, every purchase RESERVES its units
/// immediately, and completing a job consumes one unit of its cited part.
/// Nobody edits their own money state and nobody buys stock that isn't there.
/// </summary>
[Collection("ApiDb")]
public sealed class PartsEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public PartsEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListParts_DerivesStockStates_FromQuantities()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var healthy = await CreatePartAsync(client, managerToken, "Shelf Healthy", price: 40m, quantity: 10, minimum: 4);
        var low = await CreatePartAsync(client, managerToken, "Shelf Low", price: 40m, quantity: 4, minimum: 4);
        var empty = await CreatePartAsync(client, managerToken, "Shelf Empty", price: 40m, quantity: 0, minimum: 4);

        var parts = await ReadPartsAsync(client, managerToken);

        parts.Should().Contain(p => p.GetProperty("partId").GetGuid() == healthy && p.GetProperty("stockState").GetInt32() == 0);
        parts.Should().Contain(p => p.GetProperty("partId").GetGuid() == low && p.GetProperty("stockState").GetInt32() == 1);
        parts.Should().Contain(p => p.GetProperty("partId").GetGuid() == empty && p.GetProperty("stockState").GetInt32() == 2);
    }

    [Fact]
    public async Task AdjustStock_RecordsHistory_AndRejectsOversell()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var partId = await CreatePartAsync(client, managerToken, "Adjusted Shelf", price: 30m, quantity: 10, minimum: 4);

        await AdjustAsync(client, managerToken, partId, -1);
        await AdjustAsync(client, managerToken, partId, 5);
        using var rejected = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/parts/{partId}/adjust",
                new { delta = -100, reason = "Oversell test" }, managerToken),
            TestContext.Current.CancellationToken);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var parts = await ReadPartsAsync(client, managerToken);
        var part = parts.Single(p => p.GetProperty("partId").GetGuid() == partId);

        part.GetProperty("quantity").GetInt32().Should().Be(14);
        part.GetProperty("stockState").GetInt32().Should().Be(0);

        using var history = await client.SendAsync(RequestMessage(HttpMethod.Get, $"/api/parts/{partId}/movements", null, managerToken), TestContext.Current.CancellationToken);
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        var movements = await history.Content.ReadFromJsonAsync<JsonElement[]>(TestContext.Current.CancellationToken);
        movements.Should().HaveCount(3);
        movements![0].GetProperty("quantityAfter").GetInt32().Should().Be(14);
    }

    [Fact]
    public async Task DuplicatePartName_IsRejectedAsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        using var first = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/parts", new { name = $"Radiator Hose {tag}", category = 0, price = 25.0, quantity = 5, minimumQuantity = 2 }, managerToken),
            TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Different case, same shelf: still the same identity.
        using var second = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/parts", new { name = $"radiator hose {tag}", category = 0, price = 25.0, quantity = 5, minimumQuantity = 2 }, managerToken),
            TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customers_ReadTheShop_ButCannotTouchTheShelf()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var shop = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/parts", null, customerToken),
            TestContext.Current.CancellationToken);

        shop.StatusCode.Should().Be(HttpStatusCode.OK);
        shop.Content.Headers.ContentType!.ToString().Should().Contain("json");

        using var anonymous = await client.GetAsync("/api/parts", TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var partId = await CreatePartAsync(client, managerToken, "Forbidden Shelf", price: 10m, quantity: 5, minimum: 2);

        using var forbidden = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/parts/{partId}/adjust", new { delta = 1 }, customerToken),
            TestContext.Current.CancellationToken);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompletingWorkOrder_DeductsCitedPart()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var partId = await CreatePartAsync(client, managerToken, "Brake Pads Flow", price: 80m, quantity: 2, minimum: 1);

        var taskWithPart = await CreateServiceTaskAsync(client, managerToken, "Padded Job", basePrice: 70m);

        using var linked = await client.SendAsync(
            RequestMessage(
                HttpMethod.Put,
                $"/api/service-tasks/{taskWithPart}",
                new { name = $"Padded Job (parted)", basePrice = 70.0, estimatedDurationInMins = 30, partId },
                managerToken),
            TestContext.Current.CancellationToken);

        linked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (_, vehicleId) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var workOrderId = await ReadWorkOrderIdAsync(await CreateWorkOrderAsync(client, managerToken, vehicleId, [taskWithPart]));

        await AdvanceToCompletedAsync(client, managerToken, workOrderId);

        var parts = await ReadPartsAsync(client, managerToken);
        var part = parts.Single(p => p.GetProperty("partId").GetGuid() == partId);

        // 2 - 1 consumed = 1, which sits exactly at the minimum: Low stock.
        part.GetProperty("quantity").GetInt32().Should().Be(1);
        part.GetProperty("stockState").GetInt32().Should().Be(1);

        using var history = await client.SendAsync(RequestMessage(HttpMethod.Get, $"/api/parts/{partId}/movements", null, managerToken), TestContext.Current.CancellationToken);
        var movements = await history.Content.ReadFromJsonAsync<JsonElement[]>(TestContext.Current.CancellationToken);
        movements.Should().ContainSingle(movement =>
            movement.GetProperty("delta").GetInt32() == -1 &&
            movement.GetProperty("reason").GetString()!.Contains("Work order", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LinkingUnknownPart_RejectedAsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var taskId = await CreateServiceTaskAsync(client, managerToken, "Ghost Part Job", basePrice: 50m);

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Put,
                $"/api/service-tasks/{taskId}",
                new { name = "Ghost Part Job", basePrice = 50.0, estimatedDurationInMins = 30, partId = Guid.NewGuid() },
                managerToken),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CardOrder_IsRejected_WhenNoGatewayIsConfigured()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        var partId = await CreatePartAsync(client, managerToken, "Order Card Shelf", price: 60m, quantity: 10, minimum: 2);

        using var rejected = await client.SendAsync(RequestMessage(HttpMethod.Post, "/api/portal/shop/orders",
            new { items = new[] { new { partId, quantity = 3 } }, method = 0 }, customerToken),
            TestContext.Current.CancellationToken);
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var parts = await ReadPartsAsync(client, managerToken);
        parts.Single(p => p.GetProperty("partId").GetGuid() == partId)
            .GetProperty("quantity").GetInt32().Should().Be(10);

        using var history = await client.SendAsync(RequestMessage(HttpMethod.Get, $"/api/parts/{partId}/movements", null, managerToken), TestContext.Current.CancellationToken);
        var movements = await history.Content.ReadFromJsonAsync<JsonElement[]>(TestContext.Current.CancellationToken);
        movements.Should().ContainSingle(movement => movement.GetProperty("delta").GetInt32() == 10);
    }

    [Fact]
    public async Task CashOrder_ParksPending_UntilManagerRecordsReceipt()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        var partId = await CreatePartAsync(client, managerToken, "Order Cash Shelf", price: 20m, quantity: 10, minimum: 2);
        var order = await PlaceOrderAsync(client, customerToken, [(partId, 2)], method: 1);

        order.GetProperty("status").GetInt32().Should().Be(1); // CashPending
        order.GetProperty("paymentMethod").GetInt32().Should().Be(1); // Cash

        using var stockHistory = await client.SendAsync(RequestMessage(HttpMethod.Get, $"/api/parts/{partId}/movements", null, managerToken), TestContext.Current.CancellationToken);
        var movements = await stockHistory.Content.ReadFromJsonAsync<JsonElement[]>(TestContext.Current.CancellationToken);
        movements.Should().ContainSingle(movement => movement.GetProperty("delta").GetInt32() == -2);

        var orderId = order.GetProperty("orderId").GetGuid();

        using var cash = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/shop-orders/{orderId}/record-cash-payment", null, managerToken),
            TestContext.Current.CancellationToken);

        cash.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var mine = await ReadMyOrdersAsync(client, customerToken);
        mine.Single(o => o.GetProperty("orderId").GetGuid() == orderId)
            .GetProperty("status").GetInt32().Should().Be(0); // Paid

        // Paid is terminal even for the manager.
        using var again = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/shop-orders/{orderId}/record-cash-payment", null, managerToken),
            TestContext.Current.CancellationToken);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OversellingTheShelf_IsRefusedWithConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        var partId = await CreatePartAsync(client, managerToken, "Overorder Shelf", price: 15m, quantity: 2, minimum: 1);

        using var attempt = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/portal/shop/orders",
                new { items = new[] { new { partId, quantity = 5 } }, method = 0 },
                customerToken),
            TestContext.Current.CancellationToken);

        attempt.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var parts = await ReadPartsAsync(client, managerToken);
        parts.Single(p => p.GetProperty("partId").GetGuid() == partId)
            .GetProperty("quantity").GetInt32().Should().Be(2); // untouched
    }

    [Fact]
    public async Task Customers_SeeOnlyTheirOwnOrders()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, ownerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var (_, _, strangerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        var partId = await CreatePartAsync(client, managerToken, "Private Order Shelf", price: 12m, quantity: 10, minimum: 2);
        var order = await PlaceOrderAsync(client, ownerToken, [(partId, 1)], method: 1);
        var orderId = order.GetProperty("orderId").GetGuid();

        var strangerOrders = await ReadMyOrdersAsync(client, strangerToken);

        strangerOrders.Should().NotContain(o => o.GetProperty("orderId").GetGuid() == orderId);

        using var strangerCash = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/shop-orders/{orderId}/record-cash-payment", null, strangerToken),
            TestContext.Current.CancellationToken);

        strangerCash.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerWithShopOrders_RefusesDeletion()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (customerId, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        var partId = await CreatePartAsync(client, managerToken, "Anchored Shelf", price: 9m, quantity: 10, minimum: 2);
        await PlaceOrderAsync(client, customerToken, [(partId, 1)], method: 1);

        using var deletion = await client.SendAsync(
            RequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}", null, managerToken),
            TestContext.Current.CancellationToken);

        deletion.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await deletion.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Customer_HasShopOrders");
    }

    // --- shared helpers --------------------------------------------------------

    private static async Task<JsonElement[]> ReadPartsAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/parts", null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var json = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken));

        return [.. json.EnumerateArray()];
    }

    private static async Task<JsonElement[]> ReadMyOrdersAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/shop/orders", null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return [.. json.EnumerateArray()];
    }

    private static async Task<Guid> CreatePartAsync(HttpClient client, string token, string name, decimal price, int quantity, int minimum)
    {
        var attempt = _sequence++;

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/parts",
                new { name = $"{name} {attempt}", category = 0, price, quantity, minimumQuantity = minimum },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("partId")
            .GetGuid();
    }

    private static async Task AdjustAsync(HttpClient client, string token, Guid partId, int delta)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/parts/{partId}/adjust", new
            {
                delta,
                reason = delta > 0 ? "Integration test restock" : "Integration test stock-out",
            }, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> PlaceOrderAsync(HttpClient client, string token, (Guid PartId, int Quantity)[] items, int method)
    {
        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/portal/shop/orders",
                new { items = items.Select(i => new { partId = i.PartId, quantity = i.Quantity }).ToArray(), method },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> CreateServiceTaskAsync(HttpClient client, string token, string name, decimal basePrice)
    {
        var attempt = _sequence++;

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/service-tasks",
                new { name = $"{name} {attempt}", basePrice, estimatedDurationInMins = 30 },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("serviceTaskId")
            .GetGuid();
    }

    private static async Task<(Guid CustomerId, Guid VehicleId, string Token)> CreateLinkedCustomerAsync(
        HttpClient client,
        string managerToken)
    {
        return await TestCustomerFactory.RegisterWithVehicleAsync(
            client, managerToken, "Parts Tester", "parts", "Toyota", "Corolla", "PRT", "1HGCR2F59JB3");
    }

    private static async Task<(Guid CustomerId, Guid VehicleId)> CreateCustomerWithVehicleAsync(HttpClient client, string managerToken)
    {
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var email = $"parts-{Guid.NewGuid():N}@mechanicshop.test";

        using var profile = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/customers",
                new CreateCustomerRequest(
                    "Parts Tester",
                    "+201234567883",
                    email,
                    [new CreateVehicleRequest("Toyota", "Yaris", 2020, $"PRV-{tag}", $"1HGCR2F59JB4{tag}")]),
                managerToken),
            TestContext.Current.CancellationToken);

        profile.EnsureSuccessStatusCode();

        var json = await profile.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (
            json.GetProperty("customerId").GetGuid(),
            json.GetProperty("vehicles").EnumerateArray().First().GetProperty("vehicleId").GetGuid());
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderAsync(
        HttpClient client,
        string token,
        Guid vehicleId,
        Guid[] taskIds)
    {
        var attempt = _sequence++;

        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Parts Tech {attempt}", role = 0 }, token),
            TestContext.Current.CancellationToken);

        technician.EnsureSuccessStatusCode();

        var technicianId = (await technician.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("employeeId")
            .GetGuid();

        var slot = TestSlots.Next();

        return await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = taskIds, scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = slot.Spot, technicianId },
                token),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> ReadWorkOrderIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("workOrderId")
            .GetGuid();
    }

    private static async Task AdvanceToCompletedAsync(HttpClient client, string token, Guid workOrderId)
    {
        using var started = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 1 }, token),
            TestContext.Current.CancellationToken);
        started.EnsureSuccessStatusCode();

        using var completed = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, token),
            TestContext.Current.CancellationToken);
        completed.EnsureSuccessStatusCode();
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
            new { email, password },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<TokenResponseDto>(TestContext.Current.CancellationToken);

        return auth!.AccessToken;
    }
}
