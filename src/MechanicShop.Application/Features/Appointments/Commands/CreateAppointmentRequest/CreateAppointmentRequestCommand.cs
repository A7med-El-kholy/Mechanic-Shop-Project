using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Appointments.Commands.CreateAppointmentRequest;

/// <summary>
/// The portal's "request an appointment" form. The customer id is taken from the
/// token (never from the client), and "Not sure — inspection" is an absent
/// service task id.
/// </summary>
public sealed record CreateAppointmentRequestCommand(
    Guid VehicleId,
    Guid? ServiceTaskId,
    DateOnly PreferredDate)
    : IRequest<Result<Created>>;
