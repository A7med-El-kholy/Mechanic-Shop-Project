using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Customers.Contracts;

using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// The inbox contract: every business event writes its notification inside the
/// same save as the fact it describes (an inbox can never disagree with the
/// workshop); staff and customer inboxes are walls, not windows; read state is
/// an explicit target (never a racy toggle); and the badge is a scoped COUNT.
/// Low stock is raised exactly where the prototype raised it — a deduction that
/// leaves a shelf at or below its minimum.
/// </summary>
[Collection("ApiDb")]
public sealed class NotificationsEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public NotificationsEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WorkOrderLifecycle_NotifiesStaffAndCustomer_AndReadStateIsAnExplicitSet()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var taskId = await CreateServiceTaskAsync(client, managerToken, "Notified Job", 50m);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderAsync(client, managerToken, vehicleId, [taskId]));

        // Scheduled: the workshop is told about the booking.
        var staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        var assignment = staffRows.Single(n =>
            RowWorkOrderId(n) == workOrderId &&
            n.GetProperty("category").GetInt32() == 5);

        assignment.GetProperty("title").GetString().Should().Be("New assignment");
        assignment.GetProperty("isRead").GetBoolean().Should().BeFalse();

        var staffUnreadAfterCreation = await ReadStaffUnreadCountAsync(client, managerToken);

        // In Progress: the owner gets a "Repair update".
        await SetWorkOrderStateAsync(client, managerToken, workOrderId, state: 1);

        var customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        var inProgress = customerRows.Single(n => RowWorkOrderId(n) == workOrderId);

        inProgress.GetProperty("title").GetString().Should().Be("Repair update");
        inProgress.GetProperty("category").GetInt32().Should().Be(0);
        inProgress.GetProperty("message").GetString().Should().Contain("In Progress");
        inProgress.GetProperty("isRead").GetBoolean().Should().BeFalse();

        // Completed: a second update for the owner + the issued invoice for staff.
        await SetWorkOrderStateAsync(client, managerToken, workOrderId, state: 2);

        customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        customerRows.Where(n => RowWorkOrderId(n) == workOrderId).Should().HaveCount(2);
        customerRows.Should().Contain(n =>
            RowWorkOrderId(n) == workOrderId &&
            n.GetProperty("message").GetString()!.Contains("Completed"));

        staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        staffRows.Should().Contain(n =>
            RowWorkOrderId(n) == workOrderId &&
            n.GetProperty("title").GetString() == "Repair completed" &&
            n.GetProperty("category").GetInt32() == 1);

        // Read state is an explicit target, not a toggle: replaying "read" is
        // stable, and the count returns to the same number both times.
        var assignmentId = assignment.GetProperty("notificationId").GetGuid();

        await SetStaffReadStateAsync(client, managerToken, assignmentId, isRead: true);
        (await ReadStaffUnreadCountAsync(client, managerToken))
            .Should().Be(staffUnreadAfterCreation); // +1 billing, -1 assignment

        await SetStaffReadStateAsync(client, managerToken, assignmentId, isRead: false);
        (await ReadStaffUnreadCountAsync(client, managerToken))
            .Should().Be(staffUnreadAfterCreation + 1);

        await SetStaffReadStateAsync(client, managerToken, assignmentId, isRead: true);
        (await ReadStaffUnreadCountAsync(client, managerToken))
            .Should().Be(staffUnreadAfterCreation);

        // Mark-all is scoped: staff reaches zero, the customer's inbox is untouched.
        (await ReadCustomerUnreadCountAsync(client, customerToken)).Should().Be(2);

        using (var readAll = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/notifications/read-all", null, managerToken),
            TestContext.Current.CancellationToken))
        {
            readAll.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await ReadStaffUnreadCountAsync(client, managerToken)).Should().Be(0);
        (await ReadCustomerUnreadCountAsync(client, customerToken)).Should().Be(2);
    }

    [Fact]
    public async Task InvoicePayment_NotifiesBothSides_OnCardAndOnCashClosure()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        // --- Card: paid now, both sides told ---
        var cardTaskId = await CreateServiceTaskAsync(client, managerToken, "Paid By Card Job", 80m);
        var cardWorkOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderAsync(client, managerToken, vehicleId, [cardTaskId]));
        await AdvanceToCompletedAsync(client, managerToken, cardWorkOrderId);

        var (cardInvoiceId, cardInvoiceNumber) = await FindMyInvoiceAsync(client, customerToken, cardWorkOrderId);

        using (var paid = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/portal/invoices/{cardInvoiceId}/pay", new { method = 0 }, customerToken),
            TestContext.Current.CancellationToken))
        {
            paid.EnsureSuccessStatusCode();
        }

        var customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        var receipt = customerRows.Single(n =>
            RowWorkOrderId(n) == cardWorkOrderId &&
            n.GetProperty("title").GetString() == "Payment received");

        receipt.GetProperty("category").GetInt32().Should().Be(1);
        receipt.GetProperty("message").GetString().Should().Contain(cardInvoiceNumber).And.Contain("card");

        var staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        staffRows.Should().Contain(n =>
            RowWorkOrderId(n) == cardWorkOrderId &&
            n.GetProperty("title").GetString() == "Invoice paid" &&
            n.GetProperty("message").GetString()!.Contains(cardInvoiceNumber));

        // --- Cash: chosen now (staff told), closed by the manager (owner told) ---
        var cashTaskId = await CreateServiceTaskAsync(client, managerToken, "Paid By Cash Job", 60m);
        var cashWorkOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderAsync(client, managerToken, vehicleId, [cashTaskId]));
        await AdvanceToCompletedAsync(client, managerToken, cashWorkOrderId);

        var (cashInvoiceId, cashInvoiceNumber) = await FindMyInvoiceAsync(client, customerToken, cashWorkOrderId);

        using (var chosen = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/portal/invoices/{cashInvoiceId}/pay", new { method = 1 }, customerToken),
            TestContext.Current.CancellationToken))
        {
            chosen.EnsureSuccessStatusCode();
        }

        staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        staffRows.Should().Contain(n =>
            RowWorkOrderId(n) == cashWorkOrderId &&
            n.GetProperty("title").GetString() == "Cash payment chosen" &&
            n.GetProperty("message").GetString()!.Contains(cashInvoiceNumber));

        // Not paid yet: no receipt on the customer's side.
        customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        customerRows.Should().NotContain(n =>
            RowWorkOrderId(n) == cashWorkOrderId &&
            n.GetProperty("title").GetString() == "Payment received");

        using (var recorded = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/invoices/{cashInvoiceId}/record-cash-payment", null, managerToken),
            TestContext.Current.CancellationToken))
        {
            recorded.EnsureSuccessStatusCode();
        }

        customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        customerRows.Should().Contain(n =>
            RowWorkOrderId(n) == cashWorkOrderId &&
            n.GetProperty("title").GetString() == "Payment received" &&
            n.GetProperty("message").GetString()!.Contains(cashInvoiceNumber) &&
            n.GetProperty("message").GetString()!.Contains("cash"));
    }

    [Fact]
    public async Task ShopOrder_NotifiesBothSides_AndRaisesLowStockAtTheShelf()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);
        var (partId, partName) = await CreatePartWithKnownNameAsync(client, managerToken);

        // Shelf starts at 2 with a minimum of 2; buying 1 leaves it low.
        var order = await PlaceOrderAsync(client, customerToken, partId, quantity: 1, method: 1);
        var orderNumber = order.GetProperty("orderNumber").GetString()!;

        var staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        staffRows.Should().Contain(n =>
            n.GetProperty("title").GetString() == "New shop order" &&
            n.GetProperty("category").GetInt32() == 3 &&
            n.GetProperty("message").GetString()!.Contains(orderNumber));

        staffRows.Should().Contain(n =>
            n.GetProperty("title").GetString() == "Low stock" &&
            n.GetProperty("category").GetInt32() == 2 &&
            n.GetProperty("message").GetString()!.Contains(partName));

        var customerRows = await ReadCustomerNotificationsAsync(client, customerToken);
        customerRows.Should().Contain(n =>
            n.GetProperty("title").GetString() == "Order confirmed" &&
            n.GetProperty("category").GetInt32() == 3 &&
            n.GetProperty("message").GetString()!.Contains(orderNumber));
    }

    [Fact]
    public async Task InboxesAreWalls_NotWindows()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var (_, vehicleIdA, customerTokenA) = await CreateLinkedCustomerAsync(client, managerToken);
        var (_, _, customerTokenB) = await CreateLinkedCustomerAsync(client, managerToken);

        var taskId = await CreateServiceTaskAsync(client, managerToken, "Walled Job", 40m);
        var workOrderId = await ReadWorkOrderIdAsync(
            await CreateWorkOrderAsync(client, managerToken, vehicleIdA, [taskId]));
        await AdvanceToCompletedAsync(client, managerToken, workOrderId);

        // Role walls are policy-level, exactly like every other surface.
        using (var anonymous = await client.GetAsync("/api/notifications", TestContext.Current.CancellationToken))
        {
            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using (var staffOnly = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/notifications", null, customerTokenA),
            TestContext.Current.CancellationToken))
        {
            staffOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using (var customerOnly = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/notifications", null, managerToken),
            TestContext.Current.CancellationToken))
        {
            customerOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Customer B cannot touch A's row — indistinguishable from missing.
        var aRows = await ReadCustomerNotificationsAsync(client, customerTokenA);
        var aNotificationId = aRows.First(n => RowWorkOrderId(n) == workOrderId)
            .GetProperty("notificationId")
            .GetGuid();

        using (var crossRead = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/portal/notifications/{aNotificationId}/read-state", new { isRead = true }, customerTokenB),
            TestContext.Current.CancellationToken))
        {
            crossRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // A customer cannot reach into the staff inbox either.
        var staffRows = await ReadStaffNotificationsAsync(client, managerToken);
        var staffNotificationId = staffRows.First(n => RowWorkOrderId(n) == workOrderId)
            .GetProperty("notificationId")
            .GetGuid();

        using (var eaten = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/portal/notifications/{staffNotificationId}/read-state", new { isRead = true }, customerTokenA),
            TestContext.Current.CancellationToken))
        {
            eaten.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // B's inbox simply does not carry A's rows.
        var bRows = await ReadCustomerNotificationsAsync(client, customerTokenB);
        bRows.Should().NotContain(n => RowWorkOrderId(n) == workOrderId);
    }

    // --- shared helpers --------------------------------------------------------

    private static Guid? RowWorkOrderId(JsonElement row)
    {
        var workOrderId = row.GetProperty("workOrderId");

        return workOrderId.ValueKind == JsonValueKind.Null ? null : workOrderId.GetGuid();
    }

    private static async Task<JsonElement[]> ReadStaffNotificationsAsync(HttpClient client, string token)
    {
        return await ReadNotificationsPageAsync(client, token, "/api/notifications");
    }

    private static async Task<JsonElement[]> ReadCustomerNotificationsAsync(HttpClient client, string token)
    {
        return await ReadNotificationsPageAsync(client, token, "/api/portal/notifications");
    }

    private static async Task<JsonElement[]> ReadNotificationsPageAsync(HttpClient client, string token, string path)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, $"{path}?pageSize=100", null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return [.. json.GetProperty("items").EnumerateArray()];
    }

    private static async Task<int> ReadStaffUnreadCountAsync(HttpClient client, string token)
    {
        return await ReadUnreadCountAsync(client, token, "/api/notifications/unread-count");
    }

    private static async Task<int> ReadCustomerUnreadCountAsync(HttpClient client, string token)
    {
        return await ReadUnreadCountAsync(client, token, "/api/portal/notifications/unread-count");
    }

    private static async Task<int> ReadUnreadCountAsync(HttpClient client, string token, string path)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, path, null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("count")
            .GetInt32();
    }

    private static async Task SetStaffReadStateAsync(HttpClient client, string token, Guid notificationId, bool isRead)
    {
        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Put,
                $"/api/notifications/{notificationId}/read-state",
                new { isRead },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<(Guid InvoiceId, string Number)> FindMyInvoiceAsync(
        HttpClient client,
        string token,
        Guid workOrderId)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/invoices", null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var invoice = json.EnumerateArray()
            .Single(i => i.GetProperty("workOrderId").GetGuid() == workOrderId);

        return (
            invoice.GetProperty("invoiceId").GetGuid(),
            invoice.GetProperty("invoiceNumber").GetString()!);
    }

    private static async Task<(Guid PartId, string Name)> CreatePartWithKnownNameAsync(
        HttpClient client,
        string token)
    {
        var name = $"Notification Shelf {_sequence++}";

        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/parts",
                new { name, category = 0, price = 20.0, quantity = 2, minimumQuantity = 2 },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var partId = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("partId")
            .GetGuid();

        return (partId, name);
    }

    private static async Task<JsonElement> PlaceOrderAsync(
        HttpClient client,
        string token,
        Guid partId,
        int quantity,
        int method)
    {
        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/portal/shop/orders",
                new { items = new[] { new { partId, quantity } }, method },
                token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task SetWorkOrderStateAsync(
        HttpClient client,
        string token,
        Guid workOrderId,
        int state)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state }, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
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
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var email = $"notif-{Guid.NewGuid():N}@mechanicshop.test";

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Notif Tester", "01000000000"),
            TestContext.Current.CancellationToken);

        registered.EnsureSuccessStatusCode();

        var linkedToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var profile = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/customers",
                new CreateCustomerRequest(
                    "Notif Tester",
                    "+201234567891",
                    email,
                    [new CreateVehicleRequest("Toyota", "Corolla", 2022, $"NTF-{tag}", $"1HGCR2F59JB5{tag}")]),
                managerToken),
            TestContext.Current.CancellationToken);

        profile.EnsureSuccessStatusCode();

        var json = await profile.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (
            json.GetProperty("customerId").GetGuid(),
            json.GetProperty("vehicles").EnumerateArray().First().GetProperty("vehicleId").GetGuid(),
            linkedToken);
    }

    private static async Task<HttpResponseMessage> CreateWorkOrderAsync(
        HttpClient client,
        string token,
        Guid vehicleId,
        Guid[] taskIds)
    {
        var attempt = _sequence++;

        var technician = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName = $"Notif Tech {attempt}", role = 0 }, token),
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
        await SetWorkOrderStateAsync(client, token, workOrderId, state: 1);
        await SetWorkOrderStateAsync(client, token, workOrderId, state: 2);
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
