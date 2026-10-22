namespace MechanicShop.Application.Features.Invoices.Dtos;

/// <summary>Row of the invoices list: who, for which car, how much, paid?</summary>
public sealed record InvoiceSummaryDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid WorkOrderId,
    string CustomerName,
    string VehicleInfo,
    string LicensePlate,
    int Status,
    decimal TotalAmount,
    DateOnly IssuedOn,
    int? PaymentMethod);

public sealed record InvoiceLineDto(
    Guid ServiceTaskId,
    string ServiceName,
    decimal Amount);

/// <summary>Full invoice detail: frozen lines + the money math the document
/// stands on (subtotal, tax, total).</summary>
public sealed record InvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid WorkOrderId,
    string CustomerName,
    string VehicleInfo,
    string LicensePlate,
    int Status,
    int? PaymentMethod,
    DateOnly IssuedOn,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    List<InvoiceLineDto> LineItems);
