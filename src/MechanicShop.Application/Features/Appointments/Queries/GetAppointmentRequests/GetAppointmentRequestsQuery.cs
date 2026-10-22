using MechanicShop.Application.Features.Appointments.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Appointments.Queries.GetAppointmentRequests;

/// <summary>The staff review surface: all requests, pending headmost.</summary>
public sealed record GetAppointmentRequestsQuery
    : IRequest<Result<List<AppointmentRequestDto>>>;
