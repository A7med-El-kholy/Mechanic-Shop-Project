using MechanicShop.Application.Features.ServiceTasks.Dtos;

namespace MechanicShop.Application.Features.WorkOrders.Dtos;

public sealed record WorkOrderDto(
    Guid WorkOrderId,
    Guid VehicleId,
    string VehicleInfo,
    string LicensePlate,
    string CustomerName,
    int State,
    decimal TotalPrice,
    int EstimatedDurationTotalInMins,
    List<ServiceTaskDto> ServiceTasks);
