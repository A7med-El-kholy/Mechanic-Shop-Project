using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Application.Features.Employees.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

public sealed class GetEmployeesQueryHandler(IAppDbContext context)
    : IRequestHandler<GetEmployeesQuery, Result<List<EmployeeDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<EmployeeDto>>> Handle(GetEmployeesQuery query, CancellationToken cancellationToken)
    {
        // Rosters are tiny (a handful of technicians); a flat name-ordered list
        // needs no paging machinery.
        var employees = await _context.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        return employees.ToDtos();
    }
}
