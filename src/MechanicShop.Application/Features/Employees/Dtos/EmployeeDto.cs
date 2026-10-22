namespace MechanicShop.Application.Features.Employees.Dtos;

public sealed record EmployeeDto(
    Guid EmployeeId,
    string FullName,
    int Role);
