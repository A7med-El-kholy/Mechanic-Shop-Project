using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.CreateServiceTask;

public sealed record CreateServiceTaskCommand(
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins)
    : IRequest<Result<ServiceTaskDto>>;
