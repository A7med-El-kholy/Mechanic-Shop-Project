using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees.Enums;

namespace MechanicShop.Domain.Employees;

/// <summary>
/// Workshop roster entry. Distinct from Identity accounts: the roster is what
/// scheduling displays and assigns ("John S."), while logins/roles live in
/// Identity. One entry per technician name; work orders point here, never at
/// Identity rows.
/// </summary>
public sealed class Employee : Entity
{
    public string FullName { get; private set; } = string.Empty;

    public EmployeeRole Role { get; private set; }

#pragma warning disable CS8618

    private Employee()
    { }

#pragma warning restore CS8618

    private Employee(Guid id, string fullName, EmployeeRole role)
        : base(id)
    {
        FullName = fullName;
        Role = role;
    }

    public static Result<Employee> Create(Guid id, string fullName, EmployeeRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return EmployeeErrors.NameRequired;
        }

        var normalized = fullName.Trim();

        if (normalized.Length is < 2 or > 100)
        {
            return EmployeeErrors.NameLength;
        }

        var roleDefined = role is EmployeeRole.Technician or EmployeeRole.Manager;

        if (!roleDefined)
        {
            return EmployeeErrors.InvalidRole((int)role);
        }

        return new Employee(id, normalized, role);
    }
}
