using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Application.Features.Employees.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees.Enums;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Employees.Queries.GetTechnicians;

public sealed class GetTechniciansQueryHandler(IAppDbContext context)
    : IRequestHandler<GetTechniciansQuery, Result<List<EmployeeDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<EmployeeDto>>> Handle(GetTechniciansQuery query, CancellationToken cancellationToken)
    {
        var technicians = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Role == EmployeeRole.Technician)
            .OrderBy(e => e.FullName)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        return technicians.ToDtos();
    }
}
