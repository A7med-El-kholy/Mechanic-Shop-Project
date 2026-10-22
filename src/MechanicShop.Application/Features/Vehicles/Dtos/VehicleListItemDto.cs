namespace MechanicShop.Application.Features.Vehicles.Dtos;

/// <summary>Row of the staff Vehicles screen: the car plus who owns it.</summary>
public sealed record VehicleListItemDto(
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin,
    bool IsArchived,
    Guid CustomerId,
    string CustomerName);
