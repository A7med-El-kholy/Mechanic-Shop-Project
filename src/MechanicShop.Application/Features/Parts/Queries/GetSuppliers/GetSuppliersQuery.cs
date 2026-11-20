using MechanicShop.Application.Features.Parts.Dtos;
using MediatR;

namespace MechanicShop.Application.Features.Parts.Queries.GetSuppliers;

public sealed record GetSuppliersQuery : IRequest<List<SupplierDto>>;
