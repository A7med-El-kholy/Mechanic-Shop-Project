namespace MechanicShop.Application.Features.Reports.Dtos;

/// <summary>The manager's operational summary (the prototype's Reports view):
/// lifecycle KPIs over work orders, the money picture over invoices, and the
/// two breakdowns — estimated value by service and booked minutes by
/// technician. Everything is derived: nothing here is stored anywhere.</summary>
public sealed record ReportSummaryDto(
    int WorkOrderTotal,
    int WorkOrderCompleted,

    /// <summary>Completed ÷ total work orders, as a percentage rounded to two
    /// decimals. Null when the shop has no work orders at all — an honest
    /// "no data" beats a fake 0%.</summary>
    decimal? CompletionRatePercent,

    /// <summary>How many work orders the average is taken over: every order
    /// that is not Cancelled (the prototype's basis).</summary>
    int ValueBasisOrders,

    /// <summary>Σ estimated task value over those orders, BEFORE tax — the same
    /// basis the "value by service" bars use.</summary>
    decimal EstimatedValueSum,

    /// <summary>Mean order value INCLUDING the shop's sales tax, exactly the
    /// prototype's gt(w) = Σ task price × 1.15 averaged over non-cancelled
    /// orders. Null while no such orders exist.</summary>
    decimal? AverageOrderValue,

    int InvoicesTotal,
    int InvoicesPaid,

    List<ServiceValueRow> ValueByService,
    List<TechWorkloadRow> TechnicianWorkload);

/// <summary>One service's total estimated value across non-cancelled orders.</summary>
public sealed record ServiceValueRow(
    string ServiceName,
    decimal EstimatedValue);

/// <summary>One technician's booked minutes across non-cancelled orders.
/// Technicians with zero bookings still appear — an idle tech is information.</summary>
public sealed record TechWorkloadRow(
    string TechnicianName,
    int BookedMinutes);
