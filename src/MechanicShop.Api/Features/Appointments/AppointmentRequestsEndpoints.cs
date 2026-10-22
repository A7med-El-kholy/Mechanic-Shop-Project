using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Appointments.Contracts;

using MechanicShop.Application.Features.Appointments.Commands.CreateAppointmentRequest;
using MechanicShop.Application.Features.Appointments.Queries.GetAppointmentRequests;
using MechanicShop.Application.Features.Appointments.Queries.GetMyAppointmentRequests;

using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Appointments;

/// <summary>
/// Appointment REQUESTS, not bookings. Customers describe what they want and
/// when; creating the work order stays a staff action on the scheduling side.
/// Two audiences share one resource: "mine" for the caller, the full list for
/// the staff who review requests.
/// </summary>
public static class AppointmentRequestsEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentRequestsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/appointment-requests").WithTags("AppointmentRequests");

        group.MapPost("/", CreateAsync)
            .WithName("CreateAppointmentRequest")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/mine", MineAsync)
            .WithName("GetMyAppointmentRequests")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", ListAsync)
            .WithName("GetAppointmentRequests")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateAppointmentRequestRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAppointmentRequestCommand(
                request.VehicleId,
                request.ServiceTaskId,
                request.PreferredDate),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/appointment-requests/mine", result.Value);
    }

    private static async Task<IResult> MineAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyAppointmentRequestsQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetAppointmentRequestsQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }
}
