using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Parts.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Commands.CreatePurchaseOrder;
using MechanicShop.Application.Features.Parts.Commands.ReceivePurchaseOrder;
using MechanicShop.Application.Features.Parts.Queries.GetPurchaseOrders;
using MediatR;

namespace MechanicShop.Api.Features.Parts;

public static class PurchaseOrdersEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Purchase orders");
        group.MapGet("/", ListAsync).RequireAuthorization(Policies.IsStaff).WithName("GetPurchaseOrders");
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.IsManager).WithName("CreatePurchaseOrder")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{purchaseOrderId:guid}/receive", ReceiveAsync)
            .RequireAuthorization(Policies.IsManager).WithName("ReceivePurchaseOrder")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetPurchaseOrdersQuery(), cancellationToken));

    private static async Task<IResult> CreateAsync(CreatePurchaseOrderRequest request, ISender sender,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseOrderCommand(request.SupplierId,
            request.Items is null ? [] : [.. request.Items.Select(item => new PurchaseOrderItemRequest(item.PartId, item.Quantity, item.UnitCost))]);
        var result = await sender.Send(command, cancellationToken);
        return result.IsError ? result.TopError.ToProblem(httpContext) : Results.Created($"/api/purchase-orders/{result.Value.PurchaseOrderId}", result.Value);
    }

    private static async Task<IResult> ReceiveAsync(Guid purchaseOrderId, ISender sender, IUser user,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReceivePurchaseOrderCommand(purchaseOrderId, user.UserId), cancellationToken);
        return result.IsError ? result.TopError.ToProblem(httpContext) : Results.Ok(result.Value);
    }
}
