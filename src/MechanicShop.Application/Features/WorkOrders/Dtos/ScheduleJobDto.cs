namespace MechanicShop.Application.Features.WorkOrders.Dtos;

/// <summary>The schedule board's row: enough to draw a time block and open the
/// ticket, and nothing else. Starts/ends are minute-of-day on the shop clock.</summary>
public sealed record ScheduleJobDto(
    Guid WorkOrderId,
    string VehicleInfo,
    string LicensePlate,
    string CustomerName,
    Guid TechnicianId,
    string TechnicianName,
    int State,
    int StartsAtMinute,
    int EndsAtMinute,
    int Spot);
