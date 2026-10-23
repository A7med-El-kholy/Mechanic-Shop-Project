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
/// The billing contract: completing a work order ISSUES its invoice (numbered
/// INV-..., tax 15% on top of the charged services), the invoice is a money
/// snapshot that does not follow later catalog edits, and the payment story is
/// two doors — the customer pays (card now, cash at shop) and the manager
/// records received cash. Everyone can only touch their own paperwork.
/// </summary>
[Collection("ApiDb")]
public sealed class InvoicesEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public InvoicesEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompleteWorkOrder_IssuesInvoice_WithTaxMath()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var woResponse = await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 200m);
        var workOrderId = await ReadWorkOrderIdAsync(woResponse);
        // Complete the job: Scheduled -> In Progress -> Completed.
        using var started = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 1 }, managerToken),
            TestContext.Current.CancellationToken);
        started.EnsureSuccessStatusCode();
        using var completed = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, managerToken),
            TestContext.Current.CancellationToken);
        completed.EnsureSuccessStatusCode();

        using var list = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices?page=1&pageSize=50", null, managerToken),
            TestContext.Current.CancellationToken);

        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = (await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        var mine = rows.FirstOrDefault(r => r.GetProperty("workOrderId").GetGuid() == workOrderId);

        mine.ValueKind.Should().NotBe(JsonValueKind.Undefined, "completing a job must issue exactly one invoice");

        mine.GetProperty("invoiceNumber").GetString()!.Should().StartWith("INV-");
        mine.GetProperty("totalAmount").GetDouble().Should().Be(230.00); // 200 + 15%
        mine.GetProperty("status").GetInt32().Should().Be(0); // Unpaid

        // The work order screen asks "has THIS job been billed?" — the filter
        // answers with at most one row, never the whole book.
        using var filtered = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/invoices?page=1&pageSize=5&workOrderId={workOrderId}", null, managerToken),
            TestContext.Current.CancellationToken);

        filtered.StatusCode.Should().Be(HttpStatusCode.OK);

        var filteredRows = (await filtered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        filteredRows.Should().ContainSingle();
        filteredRows[0].GetProperty("workOrderId").GetGuid().Should().Be(workOrderId);
    }

    [Fact]
    public async Task CompleteWorkOrder_Twice_DoNotDuplicateInvoice()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var woResponse = await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 100m);
        var workOrderId = await ReadWorkOrderIdAsync(woResponse);

        await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 1 }, managerToken),
            TestContext.Current.CancellationToken);

        await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, managerToken),
            TestContext.Current.CancellationToken);

        // Completed is terminal: a second attempt is refused by the state machine.
        using var repeat = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, managerToken),
            TestContext.Current.CancellationToken);

        repeat.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var list = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices?page=1&pageSize=100", null, managerToken),
            TestContext.Current.CancellationToken);

        var rows = (await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        rows.Count(r => r.GetProperty("workOrderId").GetGuid() == workOrderId).Should().Be(1);
    }

    [Fact]
    public async Task InvoiceDetail_ShowsFrozenLines_AndMath()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        // Catalog price 100; AFTER invoicing it will rise to 500 — the document holds.
        var taskId = await CreateServiceTaskAsync(client, managerToken, "Frozen Service", basePrice: 100m);

        var (_, vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderWithTaskAsync(client, managerToken, vehicleId, [taskId]));

        await AdvanceToCompletedAsync(client, managerToken, workOrderId);

        using var staffList = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices?page=1&pageSize=50", null, managerToken),
            TestContext.Current.CancellationToken);

        var invoiceId = (await staffList.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .First(r => r.GetProperty("workOrderId").GetGuid() == workOrderId)
            .GetProperty("invoiceId")
            .GetGuid();

        // Catalog moves UP after the invoice exists.
        using var priceBump = await client.SendAsync(
            RequestMessage(
                HttpMethod.Put,
                $"/api/service-tasks/{taskId}",
                new { name = "Frozen Service", basePrice = 500m, estimatedDurationInMins = 30 },
                managerToken),
            TestContext.Current.CancellationToken);

        priceBump.EnsureSuccessStatusCode();

        using var detail = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/invoices/{invoiceId}", null, managerToken),
            TestContext.Current.CancellationToken);

        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoice = await detail.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        invoice.GetProperty("subtotal").GetDouble().Should().Be(100.00);
        invoice.GetProperty("taxRate").GetDouble().Should().Be(0.15);
        invoice.GetProperty("taxAmount").GetDouble().Should().Be(15.00);
        invoice.GetProperty("totalAmount").GetDouble().Should().Be(115.00);

        var lines = invoice.GetProperty("lineItems").EnumerateArray().ToList();
        lines.Should().ContainSingle();
        lines[0].GetProperty("serviceName").GetString().Should().StartWith("Frozen Service");
        lines[0].GetProperty("amount").GetDouble().Should().Be(100.00);
    }

    [Fact]
    public async Task Customer_PaysByCard_ClosesInvoice()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 80m));

        await AdvanceToCompletedAsync(client, managerToken, workOrderId);

        var invoiceId = await FindInvoiceIdAsync(client, managerToken, workOrderId);

        using var pay = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/portal/invoices/{invoiceId}/pay",
                new { method = 0 }, // card — a click, no card data
                customerToken),
            TestContext.Current.CancellationToken);

        pay.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var detail = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/portal/invoices/{invoiceId}", null, customerToken),
            TestContext.Current.CancellationToken);

        var invoice = await detail.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        invoice.GetProperty("status").GetInt32().Should().Be(1); // Paid
        invoice.GetProperty("paymentMethod").GetInt32().Should().Be(0); // Card

        // Paid is terminal even for the customer.
        using var again = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/portal/invoices/{invoiceId}/pay",
                new { method = 1 },
                customerToken),
            TestContext.Current.CancellationToken);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_ChoosesCash_ManagerRecordsReceipt()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 40m));

        await AdvanceToCompletedAsync(client, managerToken, workOrderId);
        var invoiceId = await FindInvoiceIdAsync(client, managerToken, workOrderId);

        using var chooseCash = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/portal/invoices/{invoiceId}/pay",
                new { method = 1 },
                customerToken),
            TestContext.Current.CancellationToken);

        chooseCash.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var pending = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/portal/invoices/{invoiceId}", null, customerToken),
            TestContext.Current.CancellationToken);

        var preState = await pending.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        preState.GetProperty("status").GetInt32().Should().Be(2); // CashPending

        using var cashReceived = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/invoices/{invoiceId}/record-cash-payment", null, managerToken),
            TestContext.Current.CancellationToken);

        cashReceived.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var after = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/portal/invoices/{invoiceId}", null, customerToken),
            TestContext.Current.CancellationToken);

        var paidState = await after.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        paidState.GetProperty("status").GetInt32().Should().Be(1); // Paid
        paidState.GetProperty("paymentMethod").GetInt32().Should().Be(1); // Cash
    }

    [Fact]
    public async Task Customer_CannotSee_OrPay_OtherInvoice()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, otherVehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var paidForOther = await ReadWorkOrderIdAsync(
            await CreateWorkOrderForVehicleAsync(client, managerToken, otherVehicleId, basePrice: 30m));

        await AdvanceToCompletedAsync(client, managerToken, paidForOther);
        var invoiceId = await FindInvoiceIdAsync(client, managerToken, paidForOther);

        // A second, LINKED customer must not see or pay the first car's invoice.
        var (_, _, strangerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var peek = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"/api/portal/invoices/{invoiceId}", null, strangerToken),
            TestContext.Current.CancellationToken);

        peek.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var attempt = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                $"/api/portal/invoices/{invoiceId}/pay",
                new { method = 0 },
                strangerToken),
            TestContext.Current.CancellationToken);

        attempt.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordCash_IsManagerOnly_AndCustomerListForbidden()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 25m));

        await AdvanceToCompletedAsync(client, managerToken, workOrderId);
        var invoiceId = await FindInvoiceIdAsync(client, managerToken, workOrderId);

        using var forbidden = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/invoices/{invoiceId}/record-cash-payment", null, customerToken),
            TestContext.Current.CancellationToken);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var customerList = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices", null, customerToken),
            TestContext.Current.CancellationToken);

        customerList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelledWorkOrder_DoesNotInvoice()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken);
        var woResponse = await CreateWorkOrderForVehicleAsync(client, managerToken, vehicleId, basePrice: 90m);
        var workOrderId = await ReadWorkOrderIdAsync(woResponse);

        using var cancelled = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 3 }, managerToken),
            TestContext.Current.CancellationToken);

        cancelled.EnsureSuccessStatusCode();

        using var list = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices?page=1&pageSize=100", null, managerToken),
            TestContext.Current.CancellationToken);

        var rows = (await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        rows.Count(r => r.GetProperty("workOrderId").GetGuid() == workOrderId).Should().Be(0);
    }

    // --- shared helpers --------------------------------------------------------

    private static async Task<Guid> ReadWorkOrderIdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("workOrderId")
            .GetGuid();
    }

    private static async Task AdvanceToCompletedAsync(HttpClient client, string managerToken, Guid workOrderId)
    {
        using var started = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 1 }, managerToken),
            TestContext.Current.CancellationToken);
        started.EnsureSuccessStatusCode();

        using var completed = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 2 }, managerToken),
            TestContext.Current.CancellationToken);
        completed.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> FindInvoiceIdAsync(HttpClient client, string managerToken, Guid workOrderId)
    {
        using var list = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/invoices?page=1&pageSize=100", null, managerToken),
            TestContext.Current.CancellationToken);

        var rows = (await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("items")
            .EnumerateArray()
            .ToList();

        return rows
            .First(r => r.GetProperty("workOrderId").GetGuid() == workOrderId)
            .GetProperty("invoiceId")
            .GetGuid();
    }

    private static async Task<Guid> CreateServiceTaskAsync(HttpClient client, string managerToken, string name, decimal basePrice)
    {
        var attempt = _sequence++;

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/service-tasks",
                new { name = $"{name} {attempt}", basePrice, estimatedDurationInMins = 30 },
                managerToken),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("serviceTaskId")
            .GetGuid();
    }

    private static async Task<(Guid CustomerId, Guid VehicleId, string? Token)> CreateCustomerWithVehicleAsync(
        HttpClient client,
        string managerToken)
    {
        var attempt = _sequence++;
        var email = $"billing-{attempt}@mechanicshop.test";
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        using var profile = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/customers",
                new CreateCustomerRequest(
                    "Billing Tester",
                    "+201234567880",
                    email,
                    [new CreateVehicleRequest("Nissan", "Sunny", 2019, $"BNG-{tag}", $"1HGCR2F59JB1{tag}")]),
                managerToken),
            TestContext.Current.CancellationToken);

        if (!profile.IsSuccessStatusCode)
        {
            var body = await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            throw new HttpRequestException($"profile(attr={attempt},tag={tag}) -> {(int)profile.StatusCode}: {body}");
        }

        var customer = await ReadCustomer(profile);

        return (customer!.CustomerId, customer.Vehicles[0].VehicleId, null);
    }

    private static async Task<(Guid CustomerId, Guid VehicleId, string Token)> CreateLinkedCustomerAsync(
        HttpClient client,
        string managerToken)
    {
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var email = $"billing-{Guid.NewGuid():N}@mechanicshop.test";

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Billing Tester"),
            TestContext.Current.CancellationToken);

        registered.EnsureSuccessStatusCode();

        var token = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var profile = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/customers",
                new CreateCustomerRequest(
                    "Billing Tester",
                    "+201234567881",
                    email,
                    [new CreateVehicleRequest("Honda", "Civic", 2021, $"BNG-{tag}", $"1HGCR2F59JB2{tag}")]),
                managerToken),
            TestContext.Current.CancellationToken);

        profile.EnsureSuccessStatusCode();

        var customer = await ReadCustomer(profile);

        return (customer!.CustomerId, customer.Vehicles[0].VehicleId, token);
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderForVehicleAsync(
        HttpClient client,
        string managerToken,
        Guid vehicleId,
        decimal basePrice = 60m)
    {
        var attempt = _sequence++;

        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Billing Tech {attempt}", role = 0 }, managerToken),
            TestContext.Current.CancellationToken);

        technician.EnsureSuccessStatusCode();

        var technicianId = (await technician.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("employeeId").GetGuid();

        var taskId = await CreateServiceTaskAsync(client, managerToken, "Billing Service", basePrice);

        var slot = TestSlots.Next();

        return await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = new[] { taskId }, scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = slot.Spot, technicianId },
                managerToken),
            TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderWithTaskAsync(
        HttpClient client,
        string managerToken,
        Guid vehicleId,
        Guid[] taskIds)
    {
        var attempt = _sequence++;

        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Frozen Tech {attempt}", role = 0 }, managerToken),
            TestContext.Current.CancellationToken);

        technician.EnsureSuccessStatusCode();

        var technicianId = (await technician.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("employeeId").GetGuid();

        var slot = TestSlots.Next();

        return await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/work-orders",
                new { vehicleId, serviceTaskIds = taskIds, scheduledDate = slot.Date, startsAtMinute = slot.Minute, spot = slot.Spot, technicianId },
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
