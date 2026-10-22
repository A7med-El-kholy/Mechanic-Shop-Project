using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.WorkOrders;

/// <summary>
/// A vehicle's visit: which catalog services it came in for and where the job is in
/// its lifecycle. Pricing is derived from the selected tasks — it is never stored
/// on the work order, so catalog corrections propagate to unfinished work.
/// </summary>
public sealed class WorkOrder : Entity
{
    public Guid VehicleId { get; private set; }

    public Vehicle? Vehicle { get; private set; }

    public WorkOrderState State { get; private set; }

    private readonly List<ServiceTask> _serviceTasks = [];

    public IReadOnlyList<ServiceTask> ServiceTasks => _serviceTasks.AsReadOnly();

    public decimal TotalPrice => _serviceTasks.Sum(t => t.BasePrice);

#pragma warning disable CS8618

    private WorkOrder()
    { }

#pragma warning restore CS8618

    private WorkOrder(Guid id, Guid vehicleId, WorkOrderState state, List<ServiceTask> serviceTasks)
        : base(id)
    {
        VehicleId = vehicleId;
        State = state;
        _serviceTasks = serviceTasks;
    }

    public static Result<WorkOrder> Create(Guid id, Guid vehicleId, List<ServiceTask> serviceTasks)
    {
        if (vehicleId == Guid.Empty)
        {
            return WorkOrderErrors.VehicleIdRequired;
        }

        if (serviceTasks is null || serviceTasks.Count == 0)
        {
            return WorkOrderErrors.ServiceTasksRequired;
        }

        return new WorkOrder(id, vehicleId, WorkOrderState.Scheduled, serviceTasks);
    }

    public bool IsEditable => State is not (WorkOrderState.Completed or WorkOrderState.Cancelled or WorkOrderState.InProgress);

    public bool IsActive => State is WorkOrderState.Scheduled or WorkOrderState.InProgress;

    /// <summary>
    /// The lifecycle the prototype models: a job starts Scheduled, may start work,
    /// may finish, and may be cancelled — but never revives, and a completed job
    /// can no longer be cancelled (it becomes part of history and invoices).
    /// </summary>
    public bool CanTransitionTo(WorkOrderState newState)
    {
        return (State, newState) switch
        {
            (WorkOrderState.Scheduled, WorkOrderState.InProgress) => true,
            (WorkOrderState.InProgress, WorkOrderState.Completed) => true,
            (WorkOrderState.Scheduled, WorkOrderState.Cancelled) => true,
            (WorkOrderState.InProgress, WorkOrderState.Cancelled) => true,
            _ => false
        };
    }

    public Result<Updated> UpdateState(WorkOrderState newState)
    {
        if (!CanTransitionTo(newState))
        {
            return WorkOrderErrors.InvalidStateTransition(State, newState);
        }

        State = newState;

        return Result.Updated;
    }

    public Result<Updated> UpdateServiceTasks(List<ServiceTask> serviceTasks)
    {
        if (!IsEditable)
        {
            return WorkOrderErrors.ServiceTasksReadonly(Id, State);
        }

        if (serviceTasks is null || serviceTasks.Count == 0)
        {
            return WorkOrderErrors.ServiceTasksRequired;
        }

        _serviceTasks.Clear();
        _serviceTasks.AddRange(serviceTasks);

        return Result.Updated;
    }
}
