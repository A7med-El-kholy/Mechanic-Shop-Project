using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Invoices;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Application.Features.Parts.Mappers;
using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.Inventory;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.ShopOrders.Commands.PlaceShopOrder;

public sealed class PlaceShopOrderCommandHandler(
    IAppDbContext context,
    IUser user,
    ILogger<PlaceShopOrderCommandHandler> logger)
    : IRequestHandler<PlaceShopOrderCommand, Result<PartOrderDto>>
{
    private static readonly List<PaymentMethod> ValidMethods = [PaymentMethod.Card, PaymentMethod.Cash];

    private readonly IAppDbContext _context = context;
    private readonly IUser _user = user;
    private readonly ILogger<PlaceShopOrderCommandHandler> _logger = logger;

    public async Task<Result<PartOrderDto>> Handle(PlaceShopOrderCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.UserId))
        {
            return Error.Unauthorized("Auth_NoUserClaim", "Token is missing a user identifier.");
        }

        if (!ValidMethods.Contains(command.Method))
        {
            return PartOrderErrors.MethodInvalid;
        }

        if (command.Items is null || command.Items.Count == 0)
        {
            return PartOrderErrors.ItemsRequired;
        }

        if (command.Items.Any(i => i.Quantity < 1))
        {
            return PartOrderErrors.QuantityInvalid;
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.UserId == _user.UserId, cancellationToken);

        if (customer is null)
        {
            return AppointmentRequestErrors.NoProfileLinked;
        }

        // The cart may carry the same part on several rows; aggregate first so
        // one combined checkout is judged against the shelf once.
        var requested = command.Items
            .GroupBy(i => i.PartId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var partIds = new List<Guid>(requested.Keys);

        var parts = await _context.Parts
            .Where(p => partIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (parts.Count != partIds.Count)
        {
            // The shelf changed under the open cart: the unavailable part is
            // refused with a conflict, not a 404 — the order was never placed.
            var missingId = partIds.First(id => parts.Exists(p => p.Id == id) is false);

            return PartOrderErrors.PartNotInStock($"part {missingId}");
        }

        foreach (var (partId, quantity) in requested)
        {
            var part = parts.Single(p => p.Id == partId);

            if (part.Quantity < quantity)
            {
                return PartOrderErrors.PartNotInStock(part.Name);
            }
        }

        var lines = new List<PartOrderLine>();

        foreach (var (partId, quantity) in requested)
        {
            var part = parts.Single(p => p.Id == partId);

            var created = PartOrderLine.Create(part.Id, part.Name, part.Price, quantity);

            if (created.IsError)
            {
                return created.Errors;
            }

            lines.Add(created.Value);
        }

        // Snap the prices BEFORE deducting: the document freezes what was
        // charged even if the manager re-prices the shelf two seconds later.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var order = PartOrder.Place(
            Guid.NewGuid(),
            PartOrder.NewNumber(),
            customer.Id,
            today,
            lines,
            InvoiceTaxDefaults.Rate,
            command.Method);

        if (order.IsError)
        {
            return order.Errors;
        }

        foreach (var (partId, quantity) in requested)
        {
            var part = parts.Single(p => p.Id == partId);

            var deduction = part.Adjust(-quantity);

            if (deduction.IsError)
            {
                return deduction.Errors;
            }
        }

        _context.ShopOrders.Add(order.Value);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shop order {OrderNumber} placed by customer {CustomerId}: {LineCount} line(s), {Method} — {Status}.",
            order.Value.Number,
            customer.Id,
            lines.Count,
            command.Method,
            order.Value.Status);

        return order.Value.ToDto();
    }
}
