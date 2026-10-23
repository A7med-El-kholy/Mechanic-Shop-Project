using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Customers.Contracts;

using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// The manager's operational summary: lifecycle KPIs over work orders
/// (completion rate over ALL orders, average order value over non-cancelled
/// ones — sales tax included, like the prototype's gt()), the invoices
/// paid/total ratio, estimated value grouped by service, and booked minutes
/// grouped by technician (idle techs still appear with zero). Since the test
/// database is shared, KPI assertions ride on deltas via the exposed counts,
/// while the tagged breakdown rows are asserted absolutely.
/// </summary>
[Collection("ApiDb")]
public sealed class ReportsEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private readonly ApiDbFactory _factory;

    public ReportsEndpointsTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Summary_AggregatesLifecycleMoneyAndWorkload()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var before = await ReadSummaryAsync(client, managerToken);

        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var oilChange = $"Oil Change {tag}";
        var brakeJob = $"Brake Job {tag}";
        var techA = await CreateTechnicianAsync(client, managerToken, $"Rep Alpha {tag}");
        var techB = await CreateTechnicianAsync(client, managerToken, $"Rep Beta {tag}");

        var oilTask = await CreateServiceTaskAsync(client, managerToken, oilChange, basePrice: 120.00m, durationMins: 60);
        var brakeTask = await CreateServiceTaskAsync(client, managerToken, brakeJob, basePrice: 80.00m, durationMins: 45);

        var (_, vehicleId, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        // WO1: Alpha does the oil change, pays by card — completed + PAID.
        var wo1 = await ReadWorkOrderIdAsync(await CreateWorkOrderAsync(client, managerToken, vehicleId, [oilTask], techA));
        await AdvanceToCompletedAsync(client, managerToken, wo1);
        await PayOwnInvoiceAsync(client, customerToken, wo1);

        // WO2: Beta does both jobs — completed, invoice stays unpaid.
        var wo2 = await ReadWorkOrderIdAsync(await CreateWorkOrderAsync(client, managerToken, vehicleId, [oilTask, brakeTask], techB));
        await AdvanceToCompletedAsync(client, managerToken, wo2);

        // WO3: Beta booked for brakes, job cancelled — contributes nothing
        // to value or workload, but still counts in the lifecycle rate.
        var wo3 = await ReadWorkOrderIdAsync(await CreateWorkOrderAsync(client, managerToken, vehicleId, [brakeTask], techB));
        await CancelAsync(client, managerToken, wo3);

        var after = await ReadSummaryAsync(client, managerToken);

        // Lifecycle: 3 more orders, 2 more completed, rate follows the counts.
        after.WorkOrderTotal.Should().Be(before.WorkOrderTotal + 3);
        after.WorkOrderCompleted.Should().Be(before.WorkOrderCompleted + 2);
        after.CompletionRatePercent.Should().Be(
            Math.Round((before.WorkOrderCompleted + 2) * 100m / (before.WorkOrderTotal + 3), 2));

        // Money: 2 invoices issued (auto on completion), 1 paid by card.
        after.InvoicesTotal.Should().Be(before.InvoicesTotal + 2);
        after.InvoicesPaid.Should().Be(before.InvoicesPaid + 1);

        // Estimated book: my two orders add 120 + 200 = 320 pre-tax.
        after.EstimatedValueSum.Should().Be(before.EstimatedValueSum + 320.00m);
        after.ValueBasisOrders.Should().Be(before.ValueBasisOrders + 2);

        // The prototype's KPI: mean order value INCLUDING 15% tax.
        after.AverageOrderValue.Should().Be(
            Math.Round((before.EstimatedValueSum + 320.00m) * 1.15m / (before.ValueBasisOrders + 2), 2));

        // Value by service: the oil change appears in BOTH live orders (240);
        // the brake job only in the completed one (80) — the cancelled order
        // contributes nothing.
        var serviceRows = after.ValueByService.ToDictionary(r => r.ServiceName, r => r.EstimatedValue);
        serviceRows.Should().ContainKey(oilChange).WhoseValue.Should().Be(240.00m);
        serviceRows.Should().ContainKey(brakeJob).WhoseValue.Should().Be(80.00m);

        // Technician workload: Alpha booked the 60-minute oil change; Beta
        // booked WO2's both jobs (60 + 45 = 105), and the cancelled WO3's
        // 45 minutes must NOT count.
        var workloadRows = after.TechnicianWorkload.ToDictionary(r => r.TechnicianName, r => r.BookedMinutes);
        workloadRows.Should().ContainKey($"Rep Alpha {tag}").WhoseValue.Should().Be(60);
        workloadRows.Should().ContainKey($"Rep Beta {tag}").WhoseValue.Should().Be(105);
    }

    [Fact]
    public async Task Summary_WithoutWorkOrders_ReportsNullsInsteadOfNaN()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var summary = await ReadSummaryAsync(client, managerToken);

        // The prototype divides by zero here (0/0 → NaN). The real system
        // reports honest nulls. On a shared database the counts may be
        // non-zero, so assert the invariant exactly where it is observable.
        if (summary.WorkOrderTotal == 0)
        {
            summary.CompletionRatePercent.Should().BeNull();
        }
        else
        {
            summary.CompletionRatePercent.Should().NotBeNull();
        }

        if (summary.ValueBasisOrders == 0)
        {
            summary.AverageOrderValue.Should().BeNull();
        }
        else
        {
            summary.AverageOrderValue.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Summary_IsStaffOnly()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);
        var (_, _, customerToken) = await CreateLinkedCustomerAsync(client, managerToken);

        using var anonymous = await client.GetAsync("/api/reports/summary", TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var customer = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/reports/summary", null, customerToken),
            TestContext.Current.CancellationToken);
        customer.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var staff = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/reports/summary", null, managerToken),
            TestContext.Current.CancellationToken);
        staff.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- shared helpers --------------------------------------------------------

    private static async Task<ReportSnapshot> ReadSummaryAsync(HttpClient client, string token)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/reports/summary", null, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return new ReportSnapshot(
            json.GetProperty("workOrderTotal").GetInt32(),
            json.GetProperty("workOrderCompleted").GetInt32(),
            ReadNullableDecimal(json.GetProperty("completionRatePercent")),
            json.GetProperty("valueBasisOrders").GetInt32(),
            json.GetProperty("estimatedValueSum").GetDecimal(),
            ReadNullableDecimal(json.GetProperty("averageOrderValue")),
            json.GetProperty("invoicesTotal").GetInt32(),
            json.GetProperty("invoicesPaid").GetInt32(),
            json.GetProperty("valueByService").EnumerateArray()
                .Select(r => new ServiceRow(
                    r.GetProperty("serviceName").GetString()!,
                    r.GetProperty("estimatedValue").GetDecimal()))
                .ToList(),
            json.GetProperty("technicianWorkload").EnumerateArray()
                .Select(r => new TechRow(
                    r.GetProperty("technicianName").GetString()!,
                    r.GetProperty("bookedMinutes").GetInt32()))
                .ToList());
    }

    private static decimal? ReadNullableDecimal(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetDecimal();

    private sealed record ReportSnapshot(
        int WorkOrderTotal,
        int WorkOrderCompleted,
        decimal? CompletionRatePercent,
        int ValueBasisOrders,
        decimal EstimatedValueSum,
        decimal? AverageOrderValue,
        int InvoicesTotal,
        int InvoicesPaid,
        List<ServiceRow> ValueByService,
        List<TechRow> TechnicianWorkload);

    private sealed record ServiceRow(string ServiceName, decimal EstimatedValue);

    private sealed record TechRow(string TechnicianName, int BookedMinutes);

    private static async Task<Guid> CreateTechnicianAsync(HttpClient client, string token, string fullName)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Post, "/api/employees", new { fullName, role = 0 }, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("employeeId")
            .GetGuid();
    }

    private static async Task<Guid> CreateServiceTaskAsync(HttpClient client, string token, string name, decimal basePrice, int durationMins)
    {
        using var response = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/service-tasks",
                new { name, basePrice, estimatedDurationInMins = durationMins },
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
        var email = $"reports-{Guid.NewGuid():N}@mechanicshop.test";

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", "Reports Tester"),
            TestContext.Current.CancellationToken);

        registered.EnsureSuccessStatusCode();

        var linkedToken = await GetAccessTokenAsync(client, email, "Passw0rd!");

        using var profile = await client.SendAsync(
            RequestMessage(
                HttpMethod.Post,
                "/api/customers",
                new CreateCustomerRequest(
                    "Reports Tester",
                    "+201234567884",
                    email,
                    [new CreateVehicleRequest("Honda", "Civic", 2021, $"RPT-{tag}", $"1HGCR2F59JB5{tag}")]),
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
        Guid[] taskIds,
        Guid technicianId)
    {
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

    private static async Task CancelAsync(HttpClient client, string token, Guid workOrderId)
    {
        using var response = await client.SendAsync(
            RequestMessage(HttpMethod.Put, $"/api/work-orders/{workOrderId}/status", new { state = 3 }, token),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>The owning customer pays their invoice by card — the same
    /// prototype click that marks it Paid with no card data anywhere.</summary>
    private static async Task PayOwnInvoiceAsync(HttpClient client, string customerToken, Guid workOrderId)
    {
        using var list = await client.SendAsync(
            RequestMessage(HttpMethod.Get, "/api/portal/invoices", null, customerToken),
            TestContext.Current.CancellationToken);

        list.EnsureSuccessStatusCode();

        var invoices = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var invoiceId = invoices.EnumerateArray()
            .Single(i => i.GetProperty("workOrderId").GetGuid() == workOrderId)
            .GetProperty("invoiceId")
            .GetGuid();

        using var paid = await client.SendAsync(
            RequestMessage(HttpMethod.Post, $"/api/portal/invoices/{invoiceId}/pay", new { method = 0 }, customerToken),
            TestContext.Current.CancellationToken);

        paid.EnsureSuccessStatusCode();
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
