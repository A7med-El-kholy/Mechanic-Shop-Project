using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Employees;

public static class EmployeeErrors
{
    public static Error NameRequired =>
        Error.Validation("Employee_Name_Required", "Employee name is required.");

    public static Error NameLength =>
        Error.Validation("Employee_Name_Length", "Employee name must be between 2 and 100 characters.");

    public static Error InvalidRole(int role) =>
        Error.Validation(
            "Employee_Role_Invalid",
            $"Employee role '{role}' is not supported. Use Technician (0) or Manager (1).");

    public static Error NameTaken(string name) =>
        Error.Conflict(
            "Employee_Name_Taken",
            $"An employee named '{name}' is already on the roster.");

    public static Error NotFound(Guid employeeId) =>
        Error.NotFound("Employee_NotFound", $"Employee '{employeeId}' was not found.");

    public static Error AssignedToWorkOrders(Guid employeeId) =>
        Error.Conflict(
            "Employee_AssignedToWorkOrders",
            $"Employee '{employeeId}' appears on work orders (past or scheduled) and cannot be removed from the roster.");
}
