using MechanicShop.Application.Features.Reports.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Reports.Queries.GetReportSummary;

/// <summary>The whole operational summary in one request: lifecycle KPIs,
/// invoice counts, and the two breakdowns the prototype's Reports view shows.</summary>
public sealed record GetReportSummaryQuery : IRequest<Result<ReportSummaryDto>>;
