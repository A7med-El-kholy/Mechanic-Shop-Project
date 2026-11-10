using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees.Enums;

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

    public static Error UserIdRequired =>
        Error.Validation("Employee_UserId_Required", "A user account id is required to link a roster entry.");

    public static Error AlreadyLinked(Guid employeeId) =>
        Error.Conflict(
            "Employee_AlreadyLinked",
            $"Employee '{employeeId}' is already linked to a staff account.");

    public static Error RoleMismatch(string role, EmployeeRole employeeRole) =>
        Error.Validation(
            "Employee_RoleMismatch",
            $"Staff account is '{role}', but the roster entry is a '{employeeRole}'. Provision the matching role.");
}
