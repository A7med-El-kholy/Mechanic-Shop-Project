using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers.Vehicles;

public static class VehicleErrors
{
    public static Error MakeRequired =>
        Error.Validation("Vehicle_Make_Required", "Vehicle make is required.");

    public static Error ModelRequired =>
        Error.Validation("Vehicle_Model_Required", "Vehicle model is required.");

    public static Error LicensePlateRequired =>
        Error.Validation("Vehicle_LicensePlate_Required", "Vehicle license plate is required.");

    public static Error YearInvalid =>
        Error.Validation("Vehicle_Year_Invalid", "Year must be between 1886 and next year.");

    public static Error InvalidVin =>
        Error.Validation("Vehicle_Vin_Invalid", "VIN must be exactly 17 characters, alphanumeric excluding I, O and Q.");

    public static Error DuplicateVin =>
        Error.Conflict("Vehicle_Vin_Duplicate", "A vehicle with this VIN already exists.");

    public static Error DuplicateLicensePlate =>
        Error.Conflict("Vehicle_LicensePlate_Duplicate", "A vehicle with this license plate already exists.");

    public static Error CustomerIdRequired =>
        Error.Validation("Vehicle_CustomerId_Required", "Vehicle must belong to a customer.");

    public static Error NotFound(Guid vehicleId) =>
        Error.NotFound("Vehicle_NotFound", $"Vehicle '{vehicleId}' was not found.");
}
