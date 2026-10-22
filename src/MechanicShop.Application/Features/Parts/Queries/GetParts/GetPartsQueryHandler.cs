using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Application.Features.Parts.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Parts.Queries.GetParts;

public sealed class GetPartsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetPartsQuery, Result<List<PartDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<List<PartDto>>> Handle(GetPartsQuery query, CancellationToken cancellationToken)
    {
        var parts = await _context.Parts
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return parts.ToDtos();
    }
}
