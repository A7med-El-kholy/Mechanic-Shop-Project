using MechanicShop.Application.Features.Customers.Dtos;

using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

namespace MechanicShop.Application.Features.Customers.Mappers;

public static class CustomerMapper
{
    public static CustomerDto ToDto(this Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return new CustomerDto(
            customer.Id,
            customer.Name!,
            customer.Email!,
            customer.PhoneNumber!,
            [.. customer.Vehicles.Select(v => v.ToDto())]);
    }

    public static List<CustomerDto> ToDtos(this IEnumerable<Customer> customers)
        => [.. customers.Select(c => c.ToDto())];

    public static VehicleDto ToDto(this Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return new VehicleDto(
            vehicle.Id,
            vehicle.Make!,
            vehicle.Model!,
            vehicle.Year,
            vehicle.LicensePlate!,
            vehicle.Vin,
            vehicle.VehicleInfo,
            vehicle.IsArchived);
    }

    public static List<VehicleDto> ToDtos(this IEnumerable<Vehicle> vehicles)
        => [.. vehicles.Select(v => v.ToDto())];
}
