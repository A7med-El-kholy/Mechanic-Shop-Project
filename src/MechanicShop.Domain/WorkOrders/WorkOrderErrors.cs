using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Domain.WorkOrders;

public static class WorkOrderErrors
{
    public static Error VehicleIdRequired =>
        Error.Validation("WorkOrder_VehicleId_Required", "A work order must reference a vehicle.");

    public static Error ServiceTasksRequired =>
        Error.Validation("WorkOrder_ServiceTasks_Required", "A work order must include at least one service task.");

    public static Error InvalidStateTransition(WorkOrderState current, WorkOrderState target) =>
        Error.Conflict(
            "WorkOrder_InvalidStateTransition",
            $"A work order cannot move from '{current}' to '{target}'.");

    public static Error ServiceTasksReadonly(Guid workOrderId, WorkOrderState state) =>
        Error.Conflict(
            "WorkOrder_ServiceTasks_Readonly",
            $"Work order '{workOrderId}' is in state '{state}' and its service list can no longer change.");

    public static Error NotDeletable(WorkOrderState state) =>
        Error.Conflict(
            "WorkOrder_NotDeletable",
            $"Only scheduled work orders can be deleted. Cancel or keep work order in state '{state}'.");

    public static Error VehicleMissing(Guid vehicleId) =>
        Error.NotFound("WorkOrder_Vehicle_NotFound", $"Vehicle '{vehicleId}' was not found.");

    public static Error VehicleArchived(Guid vehicleId) =>
        Error.Conflict(
            "WorkOrder_Vehicle_Archived",
            $"Vehicle '{vehicleId}' is archived. Restore it before booking new work.");

    public static Error ServiceTaskMissing(Guid serviceTaskId) =>
        Error.NotFound("WorkOrder_ServiceTask_NotFound", $"Service task '{serviceTaskId}' was not found.");

    public static Error NotFound(Guid workOrderId) =>
        Error.NotFound("WorkOrder_NotFound", $"Work order '{workOrderId}' was not found.");
}
