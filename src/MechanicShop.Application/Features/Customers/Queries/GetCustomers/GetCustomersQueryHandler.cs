using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Application.Features.Customers.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomers;

public sealed class GetCustomersQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCustomersQuery, Result<PaginatedList<CustomerDto>>>
{
    private readonly IAppDbContext _context = context;

    public async Task<Result<PaginatedList<CustomerDto>>> Handle(GetCustomersQuery query, CancellationToken cancellationToken)
    {
        // Defensive input handling: the API accepts any ints, handlers decide bounds.
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var customersQuery = _context.Customers
            .Include(c => c.Vehicles.Where(v => !v.IsArchived))
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            customersQuery = ApplySearch(customersQuery, query.Search);
        }

        // Deterministic ordering keeps Skip/Take paging stable across requests.
        customersQuery = customersQuery
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id);

        var count = await customersQuery.CountAsync(cancellationToken);

        var items = await customersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<CustomerDto>(
            items.ToDtos(),
            page,
            pageSize,
            count);
    }

    private static IQueryable<Customer> ApplySearch(IQueryable<Customer> query, string search)
    {
        var normalized = search.Trim().ToLower();

        return query.Where(c =>
            (c.Name != null && c.Name.ToLower().Contains(normalized)) ||
            (c.Email != null && c.Email.ToLower().Contains(normalized)) ||
            (c.PhoneNumber != null && c.PhoneNumber.ToLower().Contains(normalized)) ||
            c.Vehicles.Any(v => v.LicensePlate.ToLower().Contains(normalized)));
    }
}
