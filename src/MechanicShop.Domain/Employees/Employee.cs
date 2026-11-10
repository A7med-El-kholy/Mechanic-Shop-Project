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

    /// <summary>
    /// The Identity account this roster row answers to ("my work orders"), or null
    /// while the tech has no login. Soft reference like Customer.UserId: identity
    /// rows may come and go without cascading through the roster.
    /// </summary>
    public string? UserId { get; private set; }

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

    /// <summary>
    /// Binds this roster entry to a staff login. "My work orders" resolves through
    /// this link, so the hands an account answers for are whoever the roster says —
    /// never a client-supplied id.
    /// </summary>
    public Result<Updated> LinkUserAccount(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return EmployeeErrors.UserIdRequired;
        }

        UserId = userId;

        return Result.Updated;
    }
}
