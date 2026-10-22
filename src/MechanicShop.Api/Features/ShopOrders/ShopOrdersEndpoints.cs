using MechanicShop.Api.Common;

using MechanicShop.Application.Features.ShopOrders.Commands.RecordShopOrderCash;
using MechanicShop.Application.Features.ShopOrders.Queries.GetShopOrders;

using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.ShopOrders;

/// <summary>The manager's side of the parts shop: the order book and the
/// cash confirmations. Orders themselves are placed by customers in the portal.</summary>
public static class ShopOrdersEndpoints
{
    public static IEndpointRouteBuilder MapShopOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/shop-orders").WithTags("ShopOrders");

        group.MapGet("/", ListAsync)
            .WithName("GetShopOrders")
            .RequireAuthorization(Policies.IsManager)
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/{orderId:guid}/record-cash-payment", RecordCashAsync)
            .WithName("RecordShopOrderCash")
            .RequireAuthorization(Policies.IsManager)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        int page = 1,
        int pageSize = 10,
        ISender sender = null!,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetShopOrdersQuery(page, pageSize), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> RecordCashAsync(
        Guid orderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RecordShopOrderCashCommand(orderId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
