namespace MechanicShop.Application.Features.Appointments.Dtos;

/// <summary>One appointment request: what the customer asked for, for which car,
/// and the workshop's review state. Shared by the customer portal and the staff
/// review surface.</summary>
public sealed record AppointmentRequestDto(
    Guid AppointmentRequestId,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehicleInfo,
    string LicensePlate,
    Guid? ServiceTaskId,
    string? ServiceTaskName,
    decimal? ServiceTaskPrice,
    DateOnly PreferredDate,
    int Status);
