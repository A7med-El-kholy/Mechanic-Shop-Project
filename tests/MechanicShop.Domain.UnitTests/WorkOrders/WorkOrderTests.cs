using AwesomeAssertions;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.UnitTests.WorkOrders;

/// <summary>
/// The lifecycle the prototype models: Scheduled -> InProgress -> Completed, with
/// cancellation only while active. Finished history never changes state again, and
/// its service list is frozen.
/// </summary>
public sealed class WorkOrderTests
{
    private static ServiceTask SomeTask(string name = "Engine Oil Change", decimal price = 85m, int minutes = 60) =>
        ServiceTask.Create(Guid.NewGuid(), name, price, minutes).Value;

    private static List<ServiceTask> SomeTasks() =>
        [SomeTask(), SomeTask("Brake Replacement", 245m, 90)];

    [Fact]
    public void Create_WithValidInput_StartsScheduledAndHoldsTasks()
    {
        var tasks = SomeTasks();

        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), tasks);

        result.IsError.Should().BeFalse();
        result.Value.State.Should().Be(WorkOrderState.Scheduled);
        result.Value.ServiceTasks.Should().HaveCount(2);
        result.Value.TotalPrice.Should().Be(330m);
        result.Value.IsEditable.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyVehicleId_ReturnsVehicleIdRequired()
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.Empty, SomeTasks());

        result.TopError.Should().Be(WorkOrderErrors.VehicleIdRequired);
    }

    [Fact]
    public void Create_WithoutTasks_ReturnsServiceTasksRequired()
    {
        var result = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), []);

        result.TopError.Should().Be(WorkOrderErrors.ServiceTasksRequired);
    }

    [Theory]
    [InlineData(WorkOrderState.Scheduled, WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Scheduled, WorkOrderState.Cancelled)]
    [InlineData(WorkOrderState.InProgress, WorkOrderState.Completed)]
    [InlineData(WorkOrderState.InProgress, WorkOrderState.Cancelled)]
    public void UpdateState_FromActiveStates_AllowsLegalMoves(WorkOrderState from, WorkOrderState to)
    {
        var workOrder = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SomeTasks()).Value;

        if (from is WorkOrderState.InProgress)
        {
            workOrder.UpdateState(WorkOrderState.InProgress);
        }

        var result = workOrder.UpdateState(to);

        result.IsError.Should().BeFalse();
        workOrder.State.Should().Be(to);
    }

    [Theory]
    [InlineData(WorkOrderState.Scheduled, WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Completed, WorkOrderState.Scheduled)]
    [InlineData(WorkOrderState.Completed, WorkOrderState.Cancelled)]
    [InlineData(WorkOrderState.Cancelled, WorkOrderState.InProgress)]
    [InlineData(WorkOrderState.Completed, WorkOrderState.Completed)]
    public void UpdateState_FromInactiveOrSkippingStates_RejectsIllegalMoves(WorkOrderState from, WorkOrderState to)
    {
        var workOrder = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SomeTasks()).Value;

        WalkTo(workOrder, from);

        var result = workOrder.UpdateState(to);

        result.IsError.Should().BeTrue();
        result.TopError.Type.Should().Be(ErrorKind.Conflict);
        workOrder.State.Should().Be(from);
    }

    [Fact]
    public void UpdateServiceTasks_WhileScheduled_ReplacesList()
    {
        var workOrder = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SomeTasks()).Value;

        var replacement = new List<ServiceTask> { SomeTask("Tire Rotation", 45m, 45) };

        var result = workOrder.UpdateServiceTasks(replacement);

        result.IsError.Should().BeFalse();
        workOrder.ServiceTasks.Should().ContainSingle().Which.Name.Should().Be("Tire Rotation");
        workOrder.TotalPrice.Should().Be(45m);
    }

    [Fact]
    public void UpdateServiceTasks_WhenInProgress_IsRejected()
    {
        var workOrder = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SomeTasks()).Value;
        workOrder.UpdateState(WorkOrderState.InProgress);

        var result = workOrder.UpdateServiceTasks(SomeTasks());

        result.IsError.Should().BeTrue();

        result.TopError.Code.Should().Be("WorkOrder_ServiceTasks_Readonly");
        workOrder.ServiceTasks.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateServiceTasks_WithEmptyList_ReturnsServiceTasksRequired()
    {
        var workOrder = WorkOrder.Create(Guid.NewGuid(), Guid.NewGuid(), SomeTasks()).Value;

        var result = workOrder.UpdateServiceTasks([]);

        result.TopError.Should().Be(WorkOrderErrors.ServiceTasksRequired);
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
