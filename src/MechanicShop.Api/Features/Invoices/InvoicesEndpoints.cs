using MechanicShop.Api.Common;

using MechanicShop.Application.Features.Invoices.Commands.RecordCashPayment;
using MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;
using MechanicShop.Application.Features.Invoices.Queries.GetInvoices;

using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Invoices;

/// <summary>
/// The billing book for the workshop. Invoices are issued by completing work
/// orders (not by posting here); staff read them and close the cash ones.
/// </summary>
public static class InvoicesEndpoints
{
    public static IEndpointRouteBuilder MapInvoicesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapGet("/", ListAsync)
            .WithName("GetInvoices")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/{invoiceId:guid}", GetAsync)
            .WithName("GetInvoiceById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{invoiceId:guid}/record-cash-payment", RecordCashAsync)
            .WithName("RecordCashPayment")
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
        var result = await sender.Send(new GetInvoicesQuery(page, pageSize), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid invoiceId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetInvoiceByIdQuery(invoiceId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> RecordCashAsync(
        Guid invoiceId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RecordCashPaymentCommand(invoiceId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
