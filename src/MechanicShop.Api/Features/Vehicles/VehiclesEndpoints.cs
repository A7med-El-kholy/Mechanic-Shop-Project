using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Customers.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Vehicles.Commands.ArchiveVehicle;
using MechanicShop.Application.Features.Vehicles.Commands.RemoveVehicle;
using MechanicShop.Application.Features.Vehicles.Commands.RestoreVehicle;
using MechanicShop.Application.Features.Vehicles.Commands.UpdateVehicle;
using MechanicShop.Application.Features.Vehicles.Queries.GetVehicleById;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Vehicles;

/// <summary>
/// Standalone vehicle management for shop staff. Adding a vehicle is phrased as
/// "extend a customer" (POST /api/customers/{id}/vehicles) because a vehicle is
/// meaningless without an owner; the other operations route by vehicle id.
/// </summary>
public static class VehiclesEndpoints
{
    public static IEndpointRouteBuilder MapVehiclesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vehicles").WithTags("Vehicles");

        group.MapGet("/{vehicleId:guid}", GetAsync)
            .WithName("GetVehicleById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{vehicleId:guid}", UpdateAsync)
            .WithName("UpdateVehicle")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{vehicleId:guid}", DeleteAsync)
            .WithName("RemoveVehicle")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{vehicleId:guid}/archive", ArchiveAsync)
            .WithName("ArchiveVehicle")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{vehicleId:guid}/restore", RestoreAsync)
            .WithName("RestoreVehicle")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid vehicleId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehicleByIdQuery(vehicleId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateAsync(
        Guid vehicleId,
        CreateVehicleRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateVehicleCommand(
                vehicleId,
                request.Make,
                request.Model,
                request.Year,
                request.LicensePlate,
                request.Vin),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid vehicleId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveVehicleCommand(vehicleId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> ArchiveAsync(
        Guid vehicleId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ArchiveVehicleCommand(vehicleId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(
        Guid vehicleId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RestoreVehicleCommand(vehicleId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
