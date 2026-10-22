using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTaskById;

public sealed record GetServiceTaskByIdQuery(Guid ServiceTaskId)
    : IRequest<Result<ServiceTaskDto>>;
