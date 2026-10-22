using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Vehicles.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Vehicles.Queries.GetVehicles;

/// <summary>Paged, searchable vehicle list for the staff Vehicles screen.</summary>
public sealed record GetVehiclesQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    bool IncludeArchived = false)
    : IRequest<Result<PaginatedList<VehicleListItemDto>>>;
