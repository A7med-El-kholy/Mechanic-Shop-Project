using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.ServiceTasks.Dtos;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceOptions;

public sealed class GetServiceOptionsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetServiceOptionsQuery, Result<List<ServiceTaskDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<ServiceTaskDto>>> Handle(GetServiceOptionsQuery query, CancellationToken cancellationToken)
    {
        var tasks = await _context.ServiceTasks
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return tasks
            .Select(t => new ServiceTaskDto(t.Id, t.Name, t.BasePrice, (int)t.EstimatedDurationInMins))
            .ToList();
    }
}
