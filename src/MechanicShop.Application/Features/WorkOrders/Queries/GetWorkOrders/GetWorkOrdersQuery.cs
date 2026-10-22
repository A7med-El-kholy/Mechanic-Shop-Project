using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;

/// <summary>Paged, filterable work order list for the staff Work Orders screen.</summary>
public sealed record GetWorkOrdersQuery(
    int Page = 1,
    int PageSize = 10,
    int? State = null,
    string? Search = null)
    : IRequest<Result<PaginatedList<WorkOrderDto>>>;
