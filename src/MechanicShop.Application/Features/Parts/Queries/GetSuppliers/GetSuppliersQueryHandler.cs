using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Queries.GetSuppliers;

public sealed class GetSuppliersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetSuppliersQuery, List<SupplierDto>>
{
    public Task<List<SupplierDto>> Handle(GetSuppliersQuery query, CancellationToken cancellationToken) =>
        context.Suppliers.AsNoTracking().OrderBy(supplier => supplier.Name)
            .Select(supplier => new SupplierDto(supplier.Id, supplier.Name, supplier.ContactName,
                supplier.Email, supplier.Phone, supplier.IsActive))
            .ToListAsync(cancellationToken);
}
