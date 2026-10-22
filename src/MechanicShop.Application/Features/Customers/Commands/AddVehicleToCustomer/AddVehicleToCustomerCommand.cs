using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicleToCustomer;

/// <summary>Registers an additional vehicle under an existing customer.</summary>
public sealed record AddVehicleToCustomerCommand(
    Guid CustomerId,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin)
    : IRequest<Result<VehicleDto>>;
