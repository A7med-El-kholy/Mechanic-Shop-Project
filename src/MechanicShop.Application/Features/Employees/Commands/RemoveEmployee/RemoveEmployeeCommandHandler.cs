using MechanicShop.Application.Common.Interfaces;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Employees.Commands.RemoveEmployee;

public sealed class RemoveEmployeeCommandHandler(
    IAppDbContext context,
    ILogger<RemoveEmployeeCommandHandler> logger)
    : IRequestHandler<RemoveEmployeeCommand, Result<Deleted>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<RemoveEmployeeCommandHandler> _logger = logger;

    public async Task<Result<Deleted>> Handle(RemoveEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == command.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return EmployeeErrors.NotFound(command.EmployeeId);
        }

        // The FK to work orders is restrict-backed; past history (completed jobs)
        // counts as much as live bookings, so the guard checks all references.
        var assigned = await _context.WorkOrders
            .AnyAsync(w => w.TechnicianId == command.EmployeeId, cancellationToken);

        if (assigned)
        {
            _logger.LogWarning(
                "Employee {EmployeeId} removal blocked: appears on work orders.",
                command.EmployeeId);

            return EmployeeErrors.AssignedToWorkOrders(command.EmployeeId);
        }

        _context.Employees.Remove(employee);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} removed from the roster.", command.EmployeeId);

        return Result.Deleted;
    }
}
