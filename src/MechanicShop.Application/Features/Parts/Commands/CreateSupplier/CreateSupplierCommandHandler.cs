using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Commands.CreateSupplier;

public sealed class CreateSupplierCommandHandler(IAppDbContext context)
    : IRequestHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    public async Task<Result<SupplierDto>> Handle(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Name) &&
            await context.Suppliers.AnyAsync(supplier => supplier.Name == command.Name.Trim(), cancellationToken))
            return SupplierErrors.AlreadyExists(command.Name.Trim());

        var created = Supplier.Create(Guid.NewGuid(), command.Name, command.ContactName, command.Email, command.Phone);
        if (created.IsError) return created.Errors;

        context.Suppliers.Add(created.Value);
        await context.SaveChangesAsync(cancellationToken);
        var supplier = created.Value;
        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactName, supplier.Email, supplier.Phone, supplier.IsActive);
    }
}
