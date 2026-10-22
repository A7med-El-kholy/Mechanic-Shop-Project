using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceOptions;

/// <summary>Catalog read for the appointment-request form: customers see what can
/// be requested (names and prices), which is all the portal needs — creating or
/// editing the catalog stays staff-only.</summary>
public sealed record GetServiceOptionsQuery
    : IRequest<Result<List<ServiceTaskDto>>>;
