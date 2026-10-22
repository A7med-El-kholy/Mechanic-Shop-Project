using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTasks;

/// <summary>Paged, searchable service catalog for the staff Services screen.</summary>
public sealed record GetServiceTasksQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null)
    : IRequest<Result<PaginatedList<ServiceTaskDto>>>;
