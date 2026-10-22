using MechanicShop.Api.Common;
using MechanicShop.Api.Features.ServiceTasks.Contracts;
using MechanicShop.Application.Features.ServiceTasks.Commands.CreateServiceTask;
using MechanicShop.Application.Features.ServiceTasks.Commands.RemoveServiceTask;
using MechanicShop.Application.Features.ServiceTasks.Commands.UpdateServiceTask;
using MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTaskById;
using MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTasks;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.ServiceTasks;

/// <summary>
/// The service catalog: what the workshop can do, for how long, at what price.
/// Reads are staff-only for now; when customer self-booking lands, the list query
/// is what their request form will bind to.
/// </summary>
public static class ServiceTasksEndpoints
{
    public static IEndpointRouteBuilder MapServiceTasksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/service-tasks").WithTags("ServiceTasks");

        group.MapGet("/", ListAsync)
            .WithName("GetServiceTasks")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{serviceTaskId:guid}", GetAsync)
            .WithName("GetServiceTaskById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .WithName("CreateServiceTask")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{serviceTaskId:guid}", UpdateAsync)
            .WithName("UpdateServiceTask")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{serviceTaskId:guid}", DeleteAsync)
            .WithName("RemoveServiceTask")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetServiceTasksQuery(page, pageSize, search),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid serviceTaskId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetServiceTaskByIdQuery(serviceTaskId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateAsync(
        CreateServiceTaskRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateServiceTaskCommand(
                request.Name,
                request.BasePrice,
                request.EstimatedDurationInMins),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/service-tasks/{result.Value.ServiceTaskId}", result.Value);
    }

    private static async Task<IResult> UpdateAsync(
        Guid serviceTaskId,
        UpdateServiceTaskRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateServiceTaskCommand(
                serviceTaskId,
                request.Name,
                request.BasePrice,
                request.EstimatedDurationInMins),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid serviceTaskId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RemoveServiceTaskCommand(serviceTaskId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
