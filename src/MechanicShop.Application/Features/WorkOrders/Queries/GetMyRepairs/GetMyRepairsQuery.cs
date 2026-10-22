using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairs;

/// <summary>The customer portal's "My Repairs" list: newest first, own vehicles only.
/// Ownership resolves from the token; there is no client-supplied customer id.</summary>
public sealed record GetMyRepairsQuery(int Page = 1, int PageSize = 10)
    : IRequest<Result<PaginatedList<WorkOrderDto>>>;
