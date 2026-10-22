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

    public static Error HasActiveWorkOrders =>
        Error.Conflict(
            "Vehicle_ActiveWorkOrders",
            "This vehicle has active work orders (scheduled or in progress). Complete or cancel them first.");

    public static Error AlreadyArchived =>
        Error.Conflict("Vehicle_AlreadyArchived", "This vehicle is already archived.");

    public static Error NotArchived =>
        Error.Conflict("Vehicle_NotArchived", "This vehicle is not archived.");

    public static Error HasPendingRequests =>
        Error.Conflict(
            "Vehicle_PendingRequests",
            "This vehicle has pending appointment requests. Review them with the customer first.");
}
