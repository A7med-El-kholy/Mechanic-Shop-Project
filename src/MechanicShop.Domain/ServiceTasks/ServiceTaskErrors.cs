using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.ServiceTasks;

public static class ServiceTaskErrors
{
    public static Error NameRequired =>
        Error.Validation("ServiceTask_Name_Required", "Service name is required.");

    public static Error NameTooLong =>
        Error.Validation("ServiceTask_Name_TooLong", "Service name must be 150 characters or fewer.");

    public static Error DurationInvalid =>
        Error.Validation(
            "ServiceTask_Duration_Invalid",
            "Estimated duration must be a supported 15-minute step between 15 and 180.");
    public static Error BasePriceInvalid =>
        Error.Validation(
            "ServiceTask_BasePrice_Invalid",
            "Base price must be greater than 0 and at most 10000.");

    public static Error DuplicateName =>
        Error.Conflict("ServiceTask_Name_Duplicate", "A service with this name already exists.");

    public static Error NotFound(Guid serviceTaskId) =>
        Error.NotFound("ServiceTask_NotFound", $"Service task '{serviceTaskId}' was not found.");
}
