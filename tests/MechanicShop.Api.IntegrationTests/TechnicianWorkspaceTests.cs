using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace MechanicShop.Api.IntegrationTests;

/// <summary>
/// The technician workspace contract: provisioning binds an account to roster
/// hands, and after that every "my" surface is resolved from the token's roster
/// link — a technician reads, opens and advances only their own jobs, while the
/// counter actions (book, edit tasks, reschedule, delete) stay manager-only.
/// The unlinked-technician case fails loudly instead of browsing as "everybody's".
/// </summary>
[Collection("ApiDb")]
public sealed class TechnicianWorkspaceTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private static int _sequence = 1;

    private readonly ApiDbFactory _factory;

    public TechnicianWorkspaceTests(ApiDbFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Provision_WithRosterLink_AccountAnswersForHands()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);

        var (email, employeeId) = await ProvisionStaffAsync(
            client, managerToken, hands.EmployeeId, attempt);

        employeeId.Should().Be(hands.EmployeeId);

        // The account now lands inside the staff area and answers for its hands.
        var token = await GetAccessTokenAsync(client, email, StaffPassword);
        token.Should().NotBeNullOrWhiteSpace();

        using var list = await SendAsync(client, HttpMethod.Get, "/api/work-orders", null, token);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Provision_WithoutLink_CreatesRosterRow()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var fullName = $"Solo Tech TW-{attempt}";

        var (email, _) = await ProvisionStaffAsync(
            client, managerToken, employeeId: null, attempt, fullName: fullName);

        using var roster = await SendAsync(client, HttpMethod.Get, "/api/employees", null, managerToken);
        roster.EnsureSuccessStatusCode();

        var rosterJson = await roster.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var names = rosterJson
            .EnumerateArray()
            .Select(e => e.GetProperty("fullName").GetString())
            .ToList();

        names.Should().Contain(fullName);
    }

    [Fact]
    public async Task Provision_SecondAccountOnSameHands_ReturnsConflict()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);

        await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var second = await ProvisionRawAsync(
            client, managerToken, $"linked-twice-{attempt}@mechanicshop.test", "Technician", hands.EmployeeId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Provision_RoleMismatch_ReturnsValidation()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;

        // A counter-hands roster row (role 1) cannot answer for a technician login.
        var counterHands = await CreateEmployeeAsync(client, managerToken, $"Counter Hands TW-{attempt}", role: 1);

        var mismatch = await ProvisionRawAsync(
            client, managerToken, $"mismatch-{attempt}@mechanicshop.test", "Technician", counterHands.EmployeeId);

        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_AsTechnician_SeesOnlyOwnJobs()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var colleague = await CreateTechnicianAsync(client, managerToken, attempt + 500);

        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var theirs = await BookJobAsync(client, managerToken, attempt, colleague.EmployeeId);

        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var list = await SendAsync(client, HttpMethod.Get, "/api/work-orders?pageSize=100", null, token);
        list.EnsureSuccessStatusCode();

        var json = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var ids = json.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("workOrderId").GetGuid())
            .ToList();

        ids.Should().Contain(mine.WorkOrderId).And.NotContain(theirs.WorkOrderId);
    }

    [Fact]
    public async Task List_ManagerCanFilterDay_SeesOnlyThatDay()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var next = await BookJobAsync(client, managerToken, attempt + 1, hands.EmployeeId);

        var dayFilter = mine.Date.ToString("yyyy-MM-dd");

        using var list = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders?scheduledDate={dayFilter}&pageSize=100", null, managerToken);
        list.EnsureSuccessStatusCode();

        var json = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var ids = json.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("workOrderId").GetGuid())
            .ToList();

        ids.Should().Contain(mine.WorkOrderId).And.NotContain(next.WorkOrderId);
    }

    [Fact]
    public async Task Detail_AsTechnician_ForeignTicketIsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var colleague = await CreateTechnicianAsync(client, managerToken, attempt + 500);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var theirs = await BookJobAsync(client, managerToken, attempt, colleague.EmployeeId);

        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var own = await SendAsync(client, HttpMethod.Get, $"/api/work-orders/{mine.WorkOrderId}", null, token);
        own.StatusCode.Should().Be(HttpStatusCode.OK);

        using var foreign = await SendAsync(client, HttpMethod.Get, $"/api/work-orders/{theirs.WorkOrderId}", null, token);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Schedule_AsTechnician_ForeignLaneIsForbidden()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var colleague = await CreateTechnicianAsync(client, managerToken, attempt + 500);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var theirs = await BookJobAsync(client, managerToken, attempt, colleague.EmployeeId);

        var token = await GetAccessTokenAsync(client, email, StaffPassword);
        var day = mine.Date.ToString("yyyy-MM-dd");

        // A plain day fetch silently narrows to the caller's own lane.
        using var board = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders/schedule?date={day}", null, token);
        board.EnsureSuccessStatusCode();

        var boardJson = await board.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var laneIds = boardJson.EnumerateArray()
            .Select(e => e.GetProperty("technicianId").GetGuid())
            .Distinct()
            .ToList();

        laneIds.Should().ContainSingle().Which.Should().Be(hands.EmployeeId);

        // And there is no "look at his day" — the ask is refused outright.
        using var foreign = await SendAsync(
            client, HttpMethod.Get, $"/api/work-orders/schedule?date={day}&technicianId={theirs.TechnicianId}", null, token);
        foreign.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_AsTechnician_AdvancesOwnJob()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var start = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/status", new { state = 1 }, token);
        start.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var complete = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/status", new { state = 2 }, token);
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Status_AsTechnician_CannotSkipOrCancel()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var skip = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/status", new { state = 2 }, token);
        skip.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var cancel = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/status", new { state = 3 }, token);
        cancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_AsTechnician_ForeignJobIsNotFound()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var colleague = await CreateTechnicianAsync(client, managerToken, attempt + 500);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var theirs = await BookJobAsync(client, managerToken, attempt, colleague.EmployeeId);

        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var foreign = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{theirs.WorkOrderId}/status", new { state = 1 }, token);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CounterActions_AsTechnician_AreForbidden()
    {
        using var client = _factory.CreateClient();
        var managerToken = await GetAccessTokenAsync(client, ManagerEmail, ManagerPassword);

        var attempt = _sequence++;
        var hands = await CreateTechnicianAsync(client, managerToken, attempt);
        var (email, _) = await ProvisionStaffAsync(client, managerToken, hands.EmployeeId, attempt);

        var mine = await BookJobAsync(client, managerToken, attempt, hands.EmployeeId);
        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var book = await SendAsync(
            client, HttpMethod.Post, "/api/work-orders",
            new
            {
                vehicleId = Guid.NewGuid(),
                serviceTaskIds = new[] { Guid.NewGuid() },
                scheduledDate = "2027-02-01",
                startsAtMinute = 480,
                spot = 0,
                technicianId = hands.EmployeeId
            },
            token);
        book.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var tasks = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/tasks",
            new { serviceTaskIds = new[] { Guid.NewGuid() } },
            token);
        tasks.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var schedule = await SendAsync(
            client, HttpMethod.Put, $"/api/work-orders/{mine.WorkOrderId}/schedule",
            new { scheduledDate = "2027-02-02", startsAtMinute = 480, spot = 0, technicianId = hands.EmployeeId },
            token);
        schedule.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var delete = await SendAsync(
            client, HttpMethod.Delete, $"/api/work-orders/{mine.WorkOrderId}", null, token);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnlinkedTechnicianToken_CannotBrowseWorkOrders()
    {
        using var client = _factory.CreateClient();

        var attempt = _sequence++;
        var email = $"orphan-tech-{attempt}@mechanicshop.test";

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<MechanicShop.Infrastructure.Identity.AppUser>>();

            var user = new MechanicShop.Infrastructure.Identity.AppUser
            {
                UserName = email,
                Email = email,
                FullName = "Orphan Tech",
                EmailConfirmed = true
            };

            user.EmailConfirmed = true;

            var created = await users.CreateAsync(user, StaffPassword);
            created.Succeeded.Should().BeTrue();

            var role = await users.AddToRoleAsync(user, "Technician");
            role.Succeeded.Should().BeTrue();
        }

        var token = await GetAccessTokenAsync(client, email, StaffPassword);

        using var list = await SendAsync(client, HttpMethod.Get, "/api/work-orders", null, token);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Fixture helpers ─────────────────────────────────────────────────────────

    private const string StaffPassword = "TechP@ss1!";

    private static int _booking = 1;

    private sealed record BookedSlot(
        Guid WorkOrderId,
        Guid TechnicianId,
        DateOnly Date);

    private sealed record TechnicianHands(
        Guid EmployeeId,
        string FullName);

    private static async Task<TechnicianHands> CreateTechnicianAsync(
        HttpClient client,
        string managerToken,
        int attempt)
    {
        return await CreateEmployeeAsync(client, managerToken, $"Roster Tech TW-{attempt}", role: 0);
    }

    private static async Task<TechnicianHands> CreateEmployeeAsync(
        HttpClient client,
        string managerToken,
        string fullName,
        int role)
    {
        var body = new { fullName, role };

        using var response = await SendAsync(client, HttpMethod.Post, "/api/employees", body, managerToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return new TechnicianHands(json.GetProperty("employeeId").GetGuid(), fullName);
    }

    private static async Task<(string Email, Guid EmployeeId)> ProvisionStaffAsync(
        HttpClient client,
        string managerToken,
        Guid? employeeId,
        int attempt,
        string? fullName = null)
    {
        var email = $"staff-tw-{attempt}-{Guid.NewGuid():N}@mechanicshop.test";

        using var response = await ProvisionRawAsync(
            client,
            managerToken,
            email,
            "Technician",
            employeeId,
            fullName ?? $"Staff Tech TW-{attempt}");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (email, json.GetProperty("employeeId").GetGuid());
    }

    private static Task<HttpResponseMessage> ProvisionRawAsync(
        HttpClient client,
        string managerToken,
        string email,
        string role,
        Guid? employeeId,
        string? fullName = null)
    {
        var body = new
        {
            email,
            password = StaffPassword,
            fullName = fullName ?? $"Provisioned TW-{email}",
            role,
            employeeId
        };

        return SendAsync(client, HttpMethod.Post, "/api/users/staff", body, managerToken);
    }

    private static async Task<BookedSlot> BookJobAsync(
        HttpClient client,
        string managerToken,
        int attempt,
        Guid technicianId)
    {
        var (vehicleId, _) = await CreateCustomerWithVehicleAsync(client, managerToken, _booking++);
        var taskIds = new[] { await CreateTaskAsync(client, managerToken, $"TW Task {attempt}:{Guid.NewGuid():N}", 80m, 30) };

        var slot = TestSlots.Next();
        var createBody = new
        {
            vehicleId,
            serviceTaskIds = taskIds,
            scheduledDate = slot.Date.ToString("yyyy-MM-dd"),
            startsAtMinute = slot.Minute,
            spot = slot.Spot,
            technicianId
        };

        using var created = await SendAsync(client, HttpMethod.Post, "/api/work-orders", createBody, managerToken);
        created.EnsureSuccessStatusCode();

        var json = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return new BookedSlot(
            json.GetProperty("workOrderId").GetGuid(),
            technicianId,
            slot.Date);
    }

    private static async Task<(Guid VehicleId, Guid CustomerId)> CreateCustomerWithVehicleAsync(
        HttpClient client,
        string managerToken,
        int attempt)
    {
        var body = new
        {
            name = $"TW Customer {attempt}:{Guid.NewGuid():N}",
            phoneNumber = $"+20120400{attempt:D3}",
            email = $"tw-cust-{attempt}-{Guid.NewGuid():N}@mechanicshop.test",
            vehicles = new[]
            {
                new
                {
                    make = "Kia",
                    model = "Picanto",
                    year = 2020,
                    licensePlate = $"TWV-{attempt:D4}",
                    vin = $"1HGCR2F59JA7{attempt:D5}"
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

    private static Task<HttpResponseMessage> SendAsync(
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

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
}
