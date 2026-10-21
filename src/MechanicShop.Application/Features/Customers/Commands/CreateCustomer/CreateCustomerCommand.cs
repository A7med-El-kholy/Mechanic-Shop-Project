using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>Create a customer profile together with its initial vehicle(s).</summary>
public sealed record CreateCustomerCommand(
    string Name,
    string PhoneNumber,
    string Email,
    List<CreateVehiclePayload> Vehicles)
    : IRequest<Result<CustomerDto>>;

/// <summary>Vehicle fields. CustomerId is assigned by the flow, not supplied by the client.</summary>
public sealed record CreateVehiclePayload(
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin);
