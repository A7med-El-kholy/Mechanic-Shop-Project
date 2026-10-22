using MechanicShop.Api.Common;

using MechanicShop.Application.Features.Invoices.Commands.PayInvoice;
using MechanicShop.Application.Features.Invoices.Queries.GetMyInvoiceById;
using MechanicShop.Application.Features.Invoices.Queries.GetMyInvoices;
using MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceOptions;
using MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairs;
using MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairById;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;

using MediatR;

namespace MechanicShop.Api.Features.Portal;

/// <summary>The pay choice as the client sends it: 0 = card, 1 = cash. No card
/// details exist — the prototype agrees ("no card data is stored").</summary>
public sealed record PayMyInvoiceRequest(int Method);

/// <summary>
/// The customer portal's data surface. Every route resolves from the token's user
/// id to the customer profile — ids are never accepted from the client — so a
/// customer reads only their own repairs. Service options expose the catalog
/// read-only for the request form.
/// </summary>
public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/portal").WithTags("Portal");

        group.MapGet("/repairs", MyRepairsAsync)
            .WithName("GetMyRepairs")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/repairs/{workOrderId:guid}", MyRepairAsync)
            .WithName("GetMyRepairById")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/service-options", ServiceOptionsAsync)
            .WithName("GetServiceOptions")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/invoices", MyInvoicesAsync)
            .WithName("GetMyInvoices")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/invoices/{invoiceId:guid}", MyInvoiceAsync)
            .WithName("GetMyInvoiceById")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/invoices/{invoiceId:guid}/pay", PayInvoiceAsync)
            .WithName("PayMyInvoice")
            .RequireAuthorization(Policies.IsCustomer)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> MyRepairsAsync(
        int page = 1,
        int pageSize = 10,
        ISender sender = null!,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetMyRepairsQuery(page, pageSize), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> MyRepairAsync(
        Guid workOrderId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyRepairByIdQuery(workOrderId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ServiceOptionsAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetServiceOptionsQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> MyInvoicesAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyInvoicesQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> MyInvoiceAsync(
        Guid invoiceId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyInvoiceByIdQuery(invoiceId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> PayInvoiceAsync(
        Guid invoiceId,
        PayMyInvoiceRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PayInvoiceCommand(invoiceId, (PaymentMethod)request.Method),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
