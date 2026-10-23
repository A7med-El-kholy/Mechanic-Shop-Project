using MechanicShop.Api.Common;
using MechanicShop.Api.Features.WorkOrders.Contracts;
using MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;
using MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderSchedule;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderStatus;
using MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderTasks;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderById;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderSchedule;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.WorkOrders;

/// <summary>
/// Staff work order management: open a job on a vehicle, evolve its state through
/// the lifecycle, adjust its service list while it is still editable, and delete
/// it as long as work has not started.
/// </summary>
public static class WorkOrdersEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/work-orders").WithTags("WorkOrders");

        group.MapGet("/", ListAsync)
            .WithName("GetWorkOrders")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/schedule", GetScheduleAsync)
            .WithName("GetWorkOrderSchedule")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{workOrderId:guid}", GetAsync)
            .WithName("GetWorkOrderById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .WithName("CreateWorkOrder")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{workOrderId:guid}/status", UpdateStatusAsync)
            .WithName("UpdateWorkOrderStatus")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{workOrderId:guid}/tasks", UpdateTasksAsync)
            .WithName("UpdateWorkOrderTasks")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{workOrderId:guid}/schedule", UpdateScheduleAsync)
            .WithName("UpdateWorkOrderSchedule")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{workOrderId:guid}", DeleteAsync)
            .WithName("RemoveWorkOrder")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        int page = 1,
        int pageSize = 10,
        int? state = null,
        string? search = null,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetWorkOrdersQuery(page, pageSize, state, search),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid workOrderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetWorkOrderByIdQuery(workOrderId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetScheduleAsync(
        DateOnly date,
        Guid? technicianId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetWorkOrderScheduleQuery(date, technicianId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkOrderRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateWorkOrderCommand(
                request.VehicleId,
                request.ServiceTaskIds,
                request.ScheduledDate,
                request.StartsAtMinute,
                request.Spot,
                request.TechnicianId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/work-orders/{result.Value.WorkOrderId}", result.Value);
    }

    private static async Task<IResult> UpdateStatusAsync(
        Guid workOrderId,
        UpdateWorkOrderStatusRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateWorkOrderStatusCommand(workOrderId, request.State),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> UpdateTasksAsync(
        Guid workOrderId,
        UpdateWorkOrderTasksRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateWorkOrderTasksCommand(workOrderId, request.ServiceTaskIds),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> UpdateScheduleAsync(
        Guid workOrderId,
        UpdateWorkOrderScheduleRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateWorkOrderScheduleCommand(
                workOrderId,
                request.ScheduledDate,
                request.StartsAtMinute,
                request.Spot,
                request.TechnicianId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid workOrderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RemoveWorkOrderCommand(workOrderId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
