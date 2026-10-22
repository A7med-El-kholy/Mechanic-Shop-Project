using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Application.Features.ServiceTasks.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTaskById;

public sealed class GetServiceTaskByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetServiceTaskByIdQuery, Result<ServiceTaskDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<ServiceTaskDto>> Handle(GetServiceTaskByIdQuery query, CancellationToken cancellationToken)
    {
        var serviceTask = await _context.ServiceTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.ServiceTaskId, cancellationToken);

        if (serviceTask is null)
        {
            return ServiceTaskErrors.NotFound(query.ServiceTaskId);
        }

        return serviceTask.ToDto();
    }
}
