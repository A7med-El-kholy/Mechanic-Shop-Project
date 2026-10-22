using MechanicShop.Application.Features.Appointments.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Appointments.Queries.GetMyAppointmentRequests;

/// <summary>Every request this account's profile has ever sent; the portal lists
/// them from newest preferred date down.</summary>
public sealed record GetMyAppointmentRequestsQuery
    : IRequest<Result<List<AppointmentRequestDto>>>;
