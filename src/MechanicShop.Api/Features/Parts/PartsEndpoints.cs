using MechanicShop.Api.Common;

using MechanicShop.Api.Features.Parts.Contracts;
using MechanicShop.Application.Features.Parts.Commands.AdjustStock;
using MechanicShop.Application.Features.Parts.Commands.CreatePart;
using MechanicShop.Application.Features.Parts.Queries.GetParts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Queries.GetStockMovements;

using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Parts;

/// <summary>
/// The storeroom and the price list. Staff read the shelf for free;
/// touching it (new shelves, movements) is the manager's job.
/// </summary>
public static class PartsEndpoints
{
    public static IEndpointRouteBuilder MapPartsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/parts").WithTags("Parts");

        group.MapGet("/", ListAsync)
            .WithName("GetParts")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("CreatePart")
            .RequireAuthorization(Policies.IsManager)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{partId:guid}/adjust", AdjustAsync)
            .WithName("AdjustPartStock")
            .RequireAuthorization(Policies.IsManager)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{partId:guid}/movements", GetMovementsAsync)
            .WithName("GetPartStockMovements")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPartsQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateAsync(
        CreatePartRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreatePartCommand(request.Name, request.Category, request.Price, request.Quantity, request.MinimumQuantity),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/parts/{result.Value.PartId}", result.Value);
    }

    private static async Task<IResult> AdjustAsync(
        Guid partId,
        AdjustPartRequest request,
        IUser user,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AdjustStockCommand(partId, request.Delta, request.Reason, user.UserId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> GetMovementsAsync(
        Guid partId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetStockMovementsQuery(partId), cancellationToken);
        return result.IsError ? result.TopError.ToProblem(httpContext) : Results.Ok(result.Value);
    }
}
