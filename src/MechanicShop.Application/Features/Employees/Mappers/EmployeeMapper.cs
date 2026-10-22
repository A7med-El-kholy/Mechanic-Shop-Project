using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Employees;

namespace MechanicShop.Application.Features.Employees.Mappers;

public static class EmployeeMapper
{
    public static EmployeeDto ToDto(this Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new EmployeeDto(
            employee.Id,
            employee.FullName,
            (int)employee.Role);
    }

    public static List<EmployeeDto> ToDtos(this IEnumerable<Employee> employees) =>
        [.. employees.Select(e => e.ToDto())];
}
