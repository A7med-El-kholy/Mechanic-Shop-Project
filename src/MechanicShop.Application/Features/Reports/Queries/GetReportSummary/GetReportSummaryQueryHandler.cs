using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices;
using MechanicShop.Application.Features.Reports.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees.Enums;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.WorkOrders.Enums;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Reports.Queries.GetReportSummary;

/// <summary>
/// The operational summary, computed on read — no report tables, no cached
/// numbers: what the prototype's rp() does over its in-memory arrays, done
/// over the real database. Four queries in total (work orders with their
/// tasks, technicians, and two invoice counts); grouping happens in memory
/// because the bars need per-name and per-technician sums of a many-to-many
/// that SQL would express no better at this scale.
/// </summary>
public sealed class GetReportSummaryQueryHandler(IAppDbContext context)
    : IRequestHandler<GetReportSummaryQuery, Result<ReportSummaryDto>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<ReportSummaryDto>> Handle(GetReportSummaryQuery query, CancellationToken cancellationToken)
    {
        var workOrders = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.ServiceTasks)
            .ToListAsync(cancellationToken);

        // Idle technicians must appear on the workload chart with zero minutes,
        // so the roster comes from Employees, not from the work orders.
        var technicians = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Role == EmployeeRole.Technician)
            .ToListAsync(cancellationToken);

        var invoicesTotal = await _context.Invoices
            .CountAsync(cancellationToken);

        var invoicesPaid = await _context.Invoices
            .CountAsync(i => i.Status == InvoiceStatus.Paid, cancellationToken);

        var completed = workOrders.Count(w => w.State == WorkOrderState.Completed);

        // The prototype's basis: a cancelled job never happened, so it never
        // contributes value — yet it still counts in the lifecycle rate.
        var basis = workOrders.Where(w => w.State != WorkOrderState.Cancelled).ToList();

        var estimatedValueSum = basis.Sum(w => w.TotalPrice);

        var byService = basis
            .SelectMany(w => w.ServiceTasks)
            .GroupBy(t => t.Name)
            .Select(g => new ServiceValueRow(g.Key, g.Sum(t => t.BasePrice)))
            .OrderByDescending(r => r.EstimatedValue)
            .ThenBy(r => r.ServiceName)
            .ToList();

        var minutesByTechnician = basis
            .GroupBy(w => w.TechnicianId)
            .ToDictionary(g => g.Key, g => g.Sum(w => w.TotalDurationInMins));

        var workload = technicians
            .Select(t => new TechWorkloadRow(
                t.FullName,
                minutesByTechnician.GetValueOrDefault(t.Id, 0)))
            .OrderByDescending(r => r.BookedMinutes)
            .ThenBy(r => r.TechnicianName)
            .ToList();

        return new ReportSummaryDto(
            workOrders.Count,
            completed,
            workOrders.Count == 0
                ? null
                : Math.Round(completed * 100m / workOrders.Count, 2),
            basis.Count,
            estimatedValueSum,
            basis.Count == 0
                ? null
                : Math.Round(
                    basis.Sum(w => w.TotalPrice * (1m + InvoiceTaxDefaults.Rate)) / basis.Count,
                    2),
            invoicesTotal,
            invoicesPaid,
            byService,
            workload);
    }
}
