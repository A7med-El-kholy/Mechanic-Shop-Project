namespace MechanicShop.Api.Features.Employees.Contracts;

/// <summary>Body for POST /api/employees. Role is the int of EmployeeRole.</summary>
public sealed record CreateEmployeeRequest(string FullName, int Role);
