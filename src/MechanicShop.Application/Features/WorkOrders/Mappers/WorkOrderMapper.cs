using MechanicShop.Application.Features.ServiceTasks.Mappers;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.Mappers;

public static class WorkOrderMapper
{
    public static WorkOrderDto ToDto(this WorkOrder workOrder)
    {
        var vehicle = workOrder.Vehicle;

        return new WorkOrderDto(
            workOrder.Id,
            workOrder.VehicleId,
            vehicle?.VehicleInfo ?? string.Empty,
            vehicle?.LicensePlate ?? string.Empty,
            vehicle?.Customer?.Name ?? string.Empty,
            (int)workOrder.State,
            workOrder.ScheduledDate,
            workOrder.StartsAtMinute,
            workOrder.EndsAtMinute,
            (int)workOrder.Spot,
            workOrder.Technician?.FullName ?? string.Empty,
            workOrder.TotalPrice,
            workOrder.ServiceTasks.Sum(t => (int)t.EstimatedDurationInMins),
            [.. workOrder.ServiceTasks.Select(t => t.ToDto())]);
    }

    public static List<WorkOrderDto> ToDtos(this IEnumerable<WorkOrder> workOrders) =>
        [.. workOrders.Select(t => t.ToDto())];
}
