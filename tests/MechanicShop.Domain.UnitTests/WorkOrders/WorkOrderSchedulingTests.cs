using AwesomeAssertions;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Employees.Enums;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.UnitTests.WorkOrders;

/// <summary>
/// Scheduling semantics ported from the prototype: a 15-minute start grid inside
/// the 08:00–20:00 day, a derived (never stored) end time, rescheduling allowed
/// only while the job is Scheduled, and conf()-style bay/technician conflicts.
/// </summary>
public sealed class WorkOrderSchedulingTests
{
    private static readonly DateOnly Day = new(2026, 10, 26);

    private static ServiceTask SomeTask(string name = "Engine Oil Change", decimal price = 85m, int minutes = 60) =>
        ServiceTask.Create(Guid.NewGuid(), name, price, minutes).Value;

    private static List<ServiceTask> Minutes(int minutes) => [SomeTask(minutes: minutes)];

    private static WorkOrder NewOrder(
        int startMinute,
        List<ServiceTask>? tasks = null,
        Spot spot = Spot.A,
        Guid? technicianId = null,
        DateOnly? date = null)
    {
        return WorkOrder.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            tasks ?? [SomeTask()],
            date ?? Day,
            startMinute,
            spot,
            technicianId ?? Guid.NewGuid()).Value;
    }

    [Theory]
    [InlineData(480)]   // 08:00 first slot
    [InlineData(1185)]  // 19:45 last slot
    [InlineData(600)]   // 10:00
    public void Create_WithOnGridStart_Accepts(int minute)
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Minutes(90), Day, minute, Spot.B, Guid.NewGuid());

        result.IsError.Should().BeFalse();
        result.Value.StartsAtMinute.Should().Be(minute);
    }

    [Theory]
    [InlineData(479)]   // one minute too early
    [InlineData(1200)]  // at closing
    [InlineData(490)]   // off-grid
    [InlineData(-15)]
    public void Create_WithOutOfWindowOrOffGridStart_Rejects(int minute)
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Minutes(90), Day, minute, Spot.B, Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("WorkOrder_Start_NotOnGrid");
    }

    [Fact]
    public void Create_WithUnsupportedSpot_Rejects()
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Minutes(60), Day, 480, (Spot)9, Guid.NewGuid());

        result.TopError.Code.Should().Be("WorkOrder_Spot_Invalid");
    }

    [Fact]
    public void Create_WithDefaultDate_Rejects()
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Minutes(60), default, 480, Spot.A, Guid.NewGuid());

        result.TopError.Code.Should().Be("WorkOrder_ScheduleDate_Required");
    }

    [Fact]
    public void Create_WithEmptyTechnician_Rejects()
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), Minutes(60), Day, 480, Spot.A, Guid.Empty);

        result.TopError.Code.Should().Be("WorkOrder_Technician_Required");
    }

    [Fact]
    public void EndsAtMinute_IsStartPlusTotalDuration()
    {
        var order = NewOrder(540, Minutes(90));

        order.EndsAtMinute.Should().Be(630); // 09:00 → 10:30
    }

    [Fact]
    public void EndsAtMinute_FloorsAtOneGridSlot()
    {
        // Duration enum has no sub-15 values, but the prototype's Math.max(dur, 15)
        // guard stays explicit for zero-minute degenerate future inputs.
        var order = NewOrder(480, Minutes(15));

        order.EndsAtMinute.Should().Be(495);
    }

    [Fact]
    public void Reschedule_WhileScheduled_MovesEverything()
    {
        var technicianId = Guid.NewGuid();
        var order = NewOrder(480, spot: Spot.A, technicianId: technicianId);

        var result = order.Reschedule(Day.AddDays(1), 630, Spot.C, Guid.NewGuid());

        result.IsError.Should().BeFalse();
        order.ScheduledDate.Should().Be(Day.AddDays(1));
        order.StartsAtMinute.Should().Be(630);
        order.Spot.Should().Be(Spot.C);
        order.TechnicianId.Should().NotBe(technicianId);
    }

    [Theory]
    [InlineData(WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public void Reschedule_AfterWorkStartsOrCloses_Rejects(WorkOrderState from)
    {
        var order = NewOrder(480, Minutes(60));

        WalkTo(order, from);

        var result = order.Reschedule(Day.AddDays(1), 480, Spot.B, Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("WorkOrder_NotSchedulable");
    }

    [Fact]
    public void Reschedule_WithOffGridStart_Rejects()
    {
        var order = NewOrder(480);

        var result = order.Reschedule(Day, 491, Spot.A, order.TechnicianId);

        result.TopError.Code.Should().Be("WorkOrder_Start_NotOnGrid");
    }

    [Fact]
    public void FindTimeConflicts_SameTechnicianOverlapping_ReportsBooking()
    {
        var technicianId = Guid.NewGuid();
        var first = NewOrder(480, Minutes(60), technicianId: technicianId);
        var second = NewOrder(510, Minutes(60), spot: Spot.B, technicianId: technicianId);

        var conflicts = second.FindTimeConflicts([first]);

        conflicts.Should().HaveCount(1);
        conflicts[0].Should().Contain("is already booked").And.Contain("08:00–09:00");
    }

    [Fact]
    public void FindTimeConflicts_SameBayOverlapping_ReportsOccupiedBay()
    {
        var first = NewOrder(480, Minutes(60), spot: Spot.B, technicianId: Guid.NewGuid());
        var second = NewOrder(480, Minutes(60), spot: Spot.B, technicianId: Guid.NewGuid());

        var conflicts = second.FindTimeConflicts([first]);

        conflicts.Should().ContainSingle()
            .Which.Should().Contain("Bay B is occupied")
            .And.Contain(first.Id.ToString());
    }

    [Fact]
    public void FindTimeConflicts_AdjacentBlocks_DontConflict()
    {
        var first = NewOrder(480, Minutes(60), technicianId: Guid.NewGuid());
        var second = NewOrder(540, Minutes(60), spot: Spot.A, technicianId: first.TechnicianId);

        var conflicts = second.FindTimeConflicts([first]);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void FindTimeConflicts_DifferentDays_DontConflict()
    {
        var technicianId = Guid.NewGuid();
        var first = NewOrder(480, Minutes(120), technicianId: technicianId);
        var second = NewOrder(480, Minutes(120), date: Day.AddDays(1), technicianId: technicianId);

        var conflicts = second.FindTimeConflicts([first]);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void FindTimeConflicts_IgnoresSelfAndFinishedJobs()
    {
        var technicianId = Guid.NewGuid();
        var order = NewOrder(480, Minutes(60), technicianId: technicianId);

        var finished = NewOrder(480, Minutes(60), spot: Spot.A, technicianId: technicianId);
        finished.UpdateState(WorkOrderState.InProgress);
        finished.UpdateState(WorkOrderState.Completed);

        var cancelled = NewOrder(480, Minutes(60), spot: Spot.B, technicianId: technicianId);
        cancelled.UpdateState(WorkOrderState.Cancelled);

        var conflicts = order.FindTimeConflicts([order, finished, cancelled]);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void FindTimeConflicts_SameTechnicianAndBay_ReportsBoth()
    {
        var technicianId = Guid.NewGuid();
        var first = NewOrder(480, Minutes(90), spot: Spot.C, technicianId: technicianId);
        var second = NewOrder(540, Minutes(30), spot: Spot.C, technicianId: technicianId);

        var conflicts = second.FindTimeConflicts([first]);

        conflicts.Should().HaveCount(2);
    }

    [Fact]
    public void Employee_Create_WithValidName_IsTechnician()
    {
        var employee = Employee.Create(Guid.NewGuid(), "John S.", EmployeeRole.Technician).Value;

        employee.FullName.Should().Be("John S.");
        employee.Role.Should().Be(EmployeeRole.Technician);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("x")]
    public void Employee_Create_WithTooShortName_Rejects(string name)
    {
        var result = Employee.Create(Guid.NewGuid(), name, EmployeeRole.Technician);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Employee_Create_WithLongName_Rejects()
    {
        var result = Employee.Create(Guid.NewGuid(), new string('n', 101), EmployeeRole.Technician);

        result.TopError.Code.Should().Be("Employee_Name_Length");
    }

    [Fact]
    public void Employee_Create_WithUnsupportedRole_Rejects()
    {
        var result = Employee.Create(Guid.NewGuid(), "John S.", (EmployeeRole)7);

        result.TopError.Code.Should().Be("Employee_Role_Invalid");
    }

    private static void WalkTo(WorkOrder workOrder, WorkOrderState target)
    {
        switch (target)
        {
            case WorkOrderState.Scheduled:
                break;
            case WorkOrderState.InProgress:
                workOrder.UpdateState(WorkOrderState.InProgress);
                break;
            case WorkOrderState.Completed:
                workOrder.UpdateState(WorkOrderState.InProgress);
                workOrder.UpdateState(WorkOrderState.Completed);
                break;
            case WorkOrderState.Cancelled:
                workOrder.UpdateState(WorkOrderState.Cancelled);
                break;
        }
    }
}
