using MechanicShop.Application.Features.ServiceTasks.Dtos;
using MechanicShop.Domain.ServiceTasks;

namespace MechanicShop.Application.Features.ServiceTasks.Mappers;

public static class ServiceTaskMapper
{
    public static ServiceTaskDto ToDto(this ServiceTask serviceTask) =>
        new(
            serviceTask.Id,
            serviceTask.Name,
            serviceTask.BasePrice,
            (int)serviceTask.EstimatedDurationInMins);

    public static List<ServiceTaskDto> ToDtos(this IEnumerable<ServiceTask> serviceTasks) =>
        [.. serviceTasks.Select(t => t.ToDto())];
}
