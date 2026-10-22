using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Application.Features.ServiceTasks.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTasks;

public sealed class GetServiceTasksQueryHandler(IAppDbContext context)
    : IRequestHandler<GetServiceTasksQuery, Result<PaginatedList<ServiceTaskDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<ServiceTaskDto>>> Handle(GetServiceTasksQuery query, CancellationToken cancellationToken)
    {
        // Defensive input handling: the API accepts any ints, handlers decide bounds.
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var tasksQuery = _context.ServiceTasks
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalized = query.Search.Trim().ToLower();

            tasksQuery = tasksQuery.Where(t =>
                t.Name.ToLower().Contains(normalized));
        }

        // Deterministic ordering keeps Skip/Take paging stable across requests.
        tasksQuery = tasksQuery
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id);

        var count = await tasksQuery.CountAsync(cancellationToken);

        var items = await tasksQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<ServiceTaskDto>(
            items.ToDtos(),
            page,
            pageSize,
            count);
    }
}
