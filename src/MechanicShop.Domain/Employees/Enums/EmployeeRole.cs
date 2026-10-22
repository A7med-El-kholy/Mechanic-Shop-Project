namespace MechanicShop.Domain.Employees.Enums;

/// <summary>
/// Roster roles. Only Technician is used today (service assignment); Manager
/// exists so future reception/manager workflows can reuse the roster without a
/// migration.
/// </summary>
public enum EmployeeRole
{
    Technician = 0,

    Manager = 1
}
