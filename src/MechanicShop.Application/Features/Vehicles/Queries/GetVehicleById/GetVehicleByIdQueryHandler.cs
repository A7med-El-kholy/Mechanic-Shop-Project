using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Vehicles.Queries.GetVehicleById;

public sealed class GetVehicleByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<VehicleDto>> Handle(GetVehicleByIdQuery query, CancellationToken cancellationToken)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == query.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return VehicleErrors.NotFound(query.VehicleId);
        }

        return vehicle.ToDto();
    }
}
