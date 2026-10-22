using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers;

public static class CustomerErrors
{
    public static Error NameRequired =>
        Error.Validation("Customer_Name_Required", "Customer name is required.");

    public static Error InvalidPhoneNumber =>
        Error.Validation("Customer_PhoneNumber_Invalid", "Phone number must be 7-15 digits and may start with '+'.");

    public static Error EmailRequired =>
        Error.Validation("Customer_Email_Required", "Email is required.");

    public static Error EmailInvalid =>
        Error.Validation("Customer_Email_Invalid", "Email is invalid.");

    public static Error CustomerExists =>
        Error.Conflict("Customer_Email_Exists", "A customer with this email already exists.");

    public static Error UserIdRequired =>
        Error.Validation("Customer_UserId_Required", "User id is required to link a customer account.");

    public static Error NotFound(Guid customerId) =>
        Error.NotFound("Customer_NotFound", $"Customer '{customerId}' was not found.");

    public static Error HasWorkOrders(Guid customerId) =>
        Error.Conflict(
            "Customer_HasWorkOrders",
            $"Customer '{customerId}' has vehicles with work orders (active jobs or history) and cannot be removed. Remove or archive the vehicles first.");

    public static Error HasPendingRequests(Guid customerId) =>
        Error.Conflict(
            "Customer_HasPendingRequests",
            $"Customer '{customerId}' has cars with pending appointment requests and cannot be removed. Review the requests first.");
}
