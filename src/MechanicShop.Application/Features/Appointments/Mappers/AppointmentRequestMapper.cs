using MechanicShop.Application.Features.Appointments.Dtos;
using MechanicShop.Domain.Appointments;

namespace MechanicShop.Application.Features.Appointments.Mappers;

public static class AppointmentRequestMapper
{
    public static AppointmentRequestDto ToDto(this AppointmentRequest request)
    {
        var vehicle = request.Vehicle;
        var task = request.ServiceTask;

        return new AppointmentRequestDto(
            request.Id,
            request.CustomerId,
            request.Customer?.Name ?? string.Empty,
            request.VehicleId,
            vehicle?.VehicleInfo ?? string.Empty,
            vehicle?.LicensePlate ?? string.Empty,
            task?.Id,
            task?.Name,
            task?.BasePrice,
            request.PreferredDate,
            (int)request.Status);
    }

    public static List<AppointmentRequestDto> ToDtos(this IEnumerable<AppointmentRequest> requests) =>
        [.. requests.Select(r => r.ToDto())];
}
