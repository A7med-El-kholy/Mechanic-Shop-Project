namespace MechanicShop.Domain.Appointments;

using MechanicShop.Domain.Common.Results;

public static class AppointmentRequestErrors
{
    public static Error IdRequired =>
        Error.Validation("Request_IdRequired", "Request Id is required.");

    public static Error CustomerIdRequired =>
        Error.Validation("Request_CustomerIdRequired", "Customer is required.");

    public static Error VehicleIdRequired =>
        Error.Validation("Request_VehicleIdRequired", "A vehicle is required.");

    public static Error ServiceTaskIdRequired =>
        Error.Validation("Request_ServiceTaskIdRequired", "Service task id cannot be empty when provided.");

    public static Error DateInPast =>
        Error.Validation("Request_DateInPast", "The preferred date cannot be in the past.");

    public static Error NotFound(Guid requestId) =>
        Error.NotFound("Request_NotFound", $"Appointment request '{requestId}' was not found.");

    public static Error VehicleNotOwned =>
        Error.Forbidden("Request_VehicleNotOwned", "This vehicle is not available on your profile.");

    public static Error NoProfileLinked =>
        Error.NotFound(
            "Request_ProfileNotLinked",
            "No customer profile is linked to this account yet. Profiles are created by the workshop using the same email.");
}
