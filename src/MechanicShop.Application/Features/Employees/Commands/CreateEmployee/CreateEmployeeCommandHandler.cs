using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Application.Features.Employees.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Employees.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeCommandHandler(
    IAppDbContext context,
    ILogger<CreateEmployeeCommandHandler> logger)
    : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreateEmployeeCommandHandler> _logger = logger;

    public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var name = command.FullName.Trim();
        var normalized = name.ToLower();

        // Names must be unique: the scheduling dropdown, work orders and reports
        // all render this name, so homonyms would be indistinguishable.
        var nameTaken = await _context.Employees
            .AnyAsync(e => e.FullName.ToLower() == normalized, cancellationToken);

        if (nameTaken)
        {
            return EmployeeErrors.NameTaken(name);
        }

        var employeeResult = Employee.Create(
            Guid.NewGuid(),
            name,
            (EmployeeRole)command.Role);

        if (employeeResult.IsError)
        {
            return employeeResult.Errors;
        }

        _context.Employees.Add(employeeResult.Value);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsEmployeeNameUniqueViolation(exception))
        {
            _logger.LogWarning(exception, "Employee create rejected: a racing insert claimed the name.");

            return EmployeeErrors.NameTaken(name);
        }

        _logger.LogInformation("Employee {EmployeeId} ({EmployeeRole}) added to the roster.", employeeResult.Value.Id, (EmployeeRole)command.Role);

        return employeeResult.Value.ToDto();
    }
}
