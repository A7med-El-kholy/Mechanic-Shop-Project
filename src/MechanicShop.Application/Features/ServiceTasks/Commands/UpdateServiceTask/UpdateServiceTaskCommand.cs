using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.UpdateServiceTask;

public sealed record UpdateServiceTaskCommand(
    Guid ServiceTaskId,
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins)
    : IRequest<Result<Updated>>;
