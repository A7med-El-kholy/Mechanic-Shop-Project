namespace MechanicShop.Api.Features.Appointments.Contracts;

public sealed record CreateAppointmentRequestRequest(
    Guid VehicleId,
    Guid? ServiceTaskId,
    DateOnly PreferredDate);
