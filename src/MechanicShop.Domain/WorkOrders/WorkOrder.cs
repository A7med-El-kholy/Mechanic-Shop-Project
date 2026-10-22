using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.WorkOrders;

/// <summary>
/// A vehicle's visit: which catalog services it came in for, the lift and
/// technician scheduled for it, and where the job is in its lifecycle. Pricing
/// and duration are derived from the selected tasks — never stored, so catalog
/// corrections propagate to unfinished work.
/// </summary>
public sealed class WorkOrder : Entity
{
    public Guid VehicleId { get; private set; }

    public Vehicle? Vehicle { get; private set; }

    public WorkOrderState State { get; private set; }

    /// <summary>Date of the visit. Stored as a date, never a timestamp: the day
    /// is what scheduling works with and no timezone conversions are involved.</summary>
    public DateOnly ScheduledDate { get; private set; }

    /// <summary>Minute-of-day on the 15-minute grid, inside the workshop's opening window.</summary>
    public int StartsAtMinute { get; private set; }

    public Spot Spot { get; private set; }

    public Guid TechnicianId { get; private set; }

    public Employee? Technician { get; private set; }

    private readonly List<ServiceTask> _serviceTasks = [];

    public IReadOnlyList<ServiceTask> ServiceTasks => _serviceTasks.AsReadOnly();

    public decimal TotalPrice => _serviceTasks.Sum(t => t.BasePrice);

    public int TotalDurationInMins => _serviceTasks.Sum(t => (int)t.EstimatedDurationInMins);

    /// <summary>Derived end of the planned time block, floored at one grid slot
    /// like the prototype's conf() does. Never persisted.</summary>
    public int EndsAtMinute => StartsAtMinute + Math.Max(TotalDurationInMins, WorkDay.MinimumJobDurationMinutes);

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

    public static Result<WorkOrder> Create(
        Guid id,
        Guid vehicleId,
        List<ServiceTask> serviceTasks,
        DateOnly scheduledDate,
        int startsAtMinute,
        Spot spot,
        Guid technicianId)
    {
        if (vehicleId == Guid.Empty)
        {
            return WorkOrderErrors.VehicleIdRequired;
        }

        if (serviceTasks is null || serviceTasks.Count == 0)
        {
            return WorkOrderErrors.ServiceTasksRequired;
        }

        var scheduleError = ValidateSchedule(scheduledDate, startsAtMinute, spot, technicianId);

        if (scheduleError is not null)
        {
            return scheduleError;
        }

        return new WorkOrder(id, vehicleId, WorkOrderState.Scheduled, serviceTasks)
        {
            ScheduledDate = scheduledDate,
            StartsAtMinute = startsAtMinute,
            Spot = spot,
            TechnicianId = technicianId
        };
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

    /// <summary>
    /// Scheduling is decided when the job is opened and may move while the job is
    /// still Scheduled — once work starts, the booked bay and technician are a
    /// historical fact, not a plan.
    /// </summary>
    public Result<Updated> Reschedule(
        DateOnly scheduledDate,
        int startsAtMinute,
        Spot spot,
        Guid technicianId)
    {
        if (State is not WorkOrderState.Scheduled)
        {
            return WorkOrderErrors.NotSchedulable(State);
        }

        var scheduleError = ValidateSchedule(scheduledDate, startsAtMinute, spot, technicianId);

        if (scheduleError is not null)
        {
            return scheduleError;
        }

        ScheduledDate = scheduledDate;
        StartsAtMinute = startsAtMinute;
        Spot = spot;
        TechnicianId = technicianId;

        return Result.Updated;
    }

    private static Error? ValidateSchedule(DateOnly scheduledDate, int startsAtMinute, Spot spot, Guid technicianId)
    {
        if (scheduledDate == default)
        {
            return WorkOrderErrors.ScheduledDateRequired;
        }

        if (!WorkDay.IsGridSlot(startsAtMinute))
        {
            return WorkOrderErrors.StartNotOnGrid(startsAtMinute);
        }

        var spotDefined = spot is Spot.A or Spot.B or Spot.C;

        if (!spotDefined)
        {
            return WorkOrderErrors.InvalidSpot((int)spot);
        }

        if (technicianId == Guid.Empty)
        {
            return WorkOrderErrors.TechnicianRequired;
        }

        return null;
    }

    private bool TimeBlocksOverlap(WorkOrder other) =>
        ScheduledDate == other.ScheduledDate &&
        StartsAtMinute < other.EndsAtMinute &&
        other.StartsAtMinute < EndsAtMinute;

    /// <summary>
    /// The prototype's conf(): half-open interval overlap among the same day's
    /// other jobs, reported per collision — one message when the technician is
    /// double-booked, another when the bay is double-booked.
    ///
    /// Only live commitments conflict: self and Cancelled are always exempt, and
    /// Completed jobs are exempt too — the prototype's day view never overlaps
    /// history, and with real dates a finished past slot must not block new work.
    /// </summary>
    public List<string> FindTimeConflicts(IEnumerable<WorkOrder> otherOrders)
    {
        var conflicts = new List<string>();

        foreach (var other in otherOrders)
        {
            if (other.Id == Id ||
                other.State is WorkOrderState.Cancelled or WorkOrderState.Completed)
            {
                continue;
            }

            if (!TimeBlocksOverlap(other))
            {
                continue;
            }

            var range = $"{WorkDay.FormatMinute(other.StartsAtMinute)}–" +
                $"{WorkDay.FormatMinute(other.EndsAtMinute)} ({other.Id})";

            if (other.TechnicianId == TechnicianId)
            {
                var technicianName = other.Technician?.FullName ?? "the assigned technician";

                conflicts.Add($"Technician {technicianName} is already booked {range}.");
            }

            if (other.Spot == Spot)
            {
                conflicts.Add($"Bay {Spot} is occupied {range}.");
            }
        }

        return conflicts;
    }
}

