namespace MechanicShop.Api.Features.WorkOrders.Contracts;

/// <summary>Body for POST /api/work-orders. Born Scheduled on a bay, with a technician.</summary>
public sealed record CreateWorkOrderRequest(
    Guid VehicleId,
    List<Guid> ServiceTaskIds,
    DateOnly ScheduledDate,
    int StartsAtMinute,
    int Spot,
    Guid TechnicianId);

/// <summary>Body for PUT /api/work-orders/{id}/status. State is the int of WorkOrderState.</summary>
public sealed record UpdateWorkOrderStatusRequest(int State);

/// <summary>Body for PUT /api/work-orders/{id}/tasks. Replaces the whole service list.</summary>
public sealed record UpdateWorkOrderTasksRequest(List<Guid> ServiceTaskIds);

/// <summary>Body for PUT /api/work-orders/{id}/schedule. Moves day, slot, bay and technician together.</summary>
public sealed record UpdateWorkOrderScheduleRequest(
    DateOnly ScheduledDate,
    int StartsAtMinute,
    int Spot,
    Guid TechnicianId);
