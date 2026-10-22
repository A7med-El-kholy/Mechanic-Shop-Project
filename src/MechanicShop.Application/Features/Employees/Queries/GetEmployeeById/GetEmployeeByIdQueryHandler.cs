using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Application.Features.Employees.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployeeById;

public sealed class GetEmployeeByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<EmployeeDto>> Handle(GetEmployeeByIdQuery query, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return EmployeeErrors.NotFound(query.EmployeeId);
        }

        return employee.ToDto();
    }
}
