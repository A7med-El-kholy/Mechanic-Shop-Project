namespace MechanicShop.Api.Features.Appointments.Contracts;

/// <summary>
/// Urgency is the number a reported urgency enum serializes to (0=Low, 1=Medium,
/// 2=High); the domain rejects anything outside that set. Notes carry the
/// customer's own words — the assistant conversation only helps compose them.
/// </summary>
public sealed record CreateAppointmentRequestRequest(
    Guid VehicleId,
    Guid? ServiceTaskId,
    DateOnly PreferredDate,
    string? Notes = null,
    int? Urgency = null);
