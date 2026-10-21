namespace MechanicShop.Application.Features.Customers.Dtos;

/// <summary>Vehicle info shown on customer cards and vehicle lists.</summary>
public sealed record VehicleDto(
    Guid VehicleId,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin,
    string VehicleInfo);
