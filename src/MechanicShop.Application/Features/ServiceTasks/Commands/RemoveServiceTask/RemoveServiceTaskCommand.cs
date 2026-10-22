using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.RemoveServiceTask;

public sealed record RemoveServiceTaskCommand(Guid ServiceTaskId)
    : IRequest<Result<Deleted>>;
