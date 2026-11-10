using MechanicShop.Domain.Common.Results;

using MediatR;

using MechanicShop.Domain.Appointments;

namespace MechanicShop.Application.Features.Appointments.Commands.CreateAppointmentRequest;

/// <summary>
/// The portal's "request an appointment" form (and the AI triage wizard's
/// submission). The customer id is taken from the token (never from the client),
/// "Not sure — inspection" is an absent service task id, and notes/urgency carry
/// what the customer reported — for humans to review, not for anything to book.
/// </summary>
public sealed record CreateAppointmentRequestCommand(
    Guid VehicleId,
    Guid? ServiceTaskId,
    DateOnly PreferredDate,
    string? Notes = null,
    ReportedUrgency? Urgency = null)
    : IRequest<Result<Created>>;
