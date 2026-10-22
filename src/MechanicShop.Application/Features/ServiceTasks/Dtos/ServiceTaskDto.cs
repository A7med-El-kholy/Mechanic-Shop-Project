namespace MechanicShop.Application.Features.ServiceTasks.Dtos;

public sealed record ServiceTaskDto(
    Guid ServiceTaskId,
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins);
