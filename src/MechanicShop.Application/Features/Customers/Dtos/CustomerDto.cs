namespace MechanicShop.Application.Features.Customers.Dtos;

/// <summary>Customer profile with its vehicles — the shape every customers endpoint returns.</summary>
public sealed record CustomerDto(
    Guid CustomerId,
    string Name,
    string Email,
    string PhoneNumber,
    IReadOnlyList<VehicleDto> Vehicles);
