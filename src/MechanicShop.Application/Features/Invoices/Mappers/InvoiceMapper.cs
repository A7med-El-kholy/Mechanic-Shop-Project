using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.Invoices.Mappers;

public static class InvoiceMapper
{
    private static (string CustomerName, string VehicleInfo, string LicensePlate) Labels(WorkOrder? workOrder)
    {
        var vehicle = workOrder?.Vehicle;

        return (
            vehicle?.Customer?.Name ?? string.Empty,
            vehicle?.VehicleInfo ?? string.Empty,
            vehicle?.LicensePlate ?? string.Empty);
    }

    public static InvoiceSummaryDto ToSummaryDto(this Invoice invoice)
    {
        var (customer, vehicleInfo, plate) = Labels(invoice.WorkOrder);

        return new InvoiceSummaryDto(
            invoice.Id,
            invoice.Number,
            invoice.WorkOrderId,
            customer,
            vehicleInfo,
            plate,
            (int)invoice.Status,
            invoice.TotalAmount,
            invoice.IssuedOn,
            invoice.PaymentMethod is null ? null : (int)invoice.PaymentMethod);
    }

    public static List<InvoiceSummaryDto> ToSummaryDtos(this IEnumerable<Invoice> invoices) =>
        [.. invoices.Select(ToSummaryDto)];

    public static InvoiceDto ToDto(this Invoice invoice)
    {
        var (customer, vehicleInfo, plate) = Labels(invoice.WorkOrder);

        return new InvoiceDto(
            invoice.Id,
            invoice.Number,
            invoice.WorkOrderId,
            customer,
            vehicleInfo,
            plate,
            (int)invoice.Status,
            invoice.PaymentMethod is null ? null : (int)invoice.PaymentMethod,
            invoice.IssuedOn,
            invoice.Subtotal,
            invoice.TaxRate,
            invoice.TaxAmount,
            invoice.TotalAmount,
            [.. invoice.LineItems.Select(li => new InvoiceLineDto(li.ServiceTaskId, li.ServiceName, li.Amount))]);
    }
}
