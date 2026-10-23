using MechanicShop.Api.Common;

using MechanicShop.Application.Features.Reports.Queries.GetReportSummary;

using MediatR;

namespace MechanicShop.Api.Features.Reports;

/// <summary>
/// The manager's Reports page. Read-only by design: every number is derived
/// from live data, so there is nothing to edit, export, or reconcile.
/// </summary>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("/summary", SummaryAsync)
            .WithName("GetReportSummary")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> SummaryAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReportSummaryQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }
}
