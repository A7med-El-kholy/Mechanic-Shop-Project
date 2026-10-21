namespace MechanicShop.Api.Features.Customers.Contracts;

/// <summary>Body for POST /api/customers. A customer is born with at least one vehicle.</summary>
public sealed record CreateCustomerRequest(
    string Name,
    string PhoneNumber,
    string Email,
    List<CreateVehicleRequest> Vehicles);

public sealed record CreateVehicleRequest(
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Vin);

/// <summary>Body for PUT /api/customers/{id}. Vehicle edits are a separate flow.</summary>
public sealed record UpdateCustomerRequest(
    string Name,
    string Email,
    string PhoneNumber);
