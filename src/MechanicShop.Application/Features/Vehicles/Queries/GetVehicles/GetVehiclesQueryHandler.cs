using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Vehicles.Dtos;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Vehicles.Queries.GetVehicles;

/// <summary>
/// The Vehicles screen's data source. Archived cars stay invisible until the UI
/// asks for them, so the list answers "what can the shop still call in for
/// service?" by default; the toggle answers "what ever arrived here?".
/// </summary>
public sealed class GetVehiclesQueryHandler(IAppDbContext context)
    : IRequestHandler<GetVehiclesQuery, Result<PaginatedList<VehicleListItemDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<VehicleListItemDto>>> Handle(GetVehiclesQuery query, CancellationToken cancellationToken)
    {
        var vehiclesQuery = _context.Vehicles.AsNoTracking();

        if (!query.IncludeArchived)
        {
            vehiclesQuery = vehiclesQuery.Where(v => !v.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalized = query.Search.Trim().ToLower();

            vehiclesQuery = vehiclesQuery.Where(v =>
                v.LicensePlate.ToLower().Contains(normalized) ||
                v.Vin.ToLower().Contains(normalized) ||
                (v.Customer != null && v.Customer.Name.ToLower().Contains(normalized)));
        }

        var count = await vehiclesQuery.CountAsync(cancellationToken);

        var items = await vehiclesQuery
            .OrderBy(v => v.LicensePlate)
            .ThenBy(v => v.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(v => new VehicleListItemDto(
                v.Id,
                v.Make,
                v.Model,
                v.Year,
                v.LicensePlate,
                v.Vin,
                v.IsArchived,
                v.CustomerId,
                v.Customer != null ? v.Customer.Name : string.Empty))
            .ToListAsync(cancellationToken);

        return new PaginatedList<VehicleListItemDto>(
            items,
            query.Page,
            query.PageSize,
            count);
    }
}
