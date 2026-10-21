using MechanicShop.Application.Common.Models;
using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Customers.Queries.GetCustomers;

/// <summary>Paged, searchable customer list for the staff Customers screen.</summary>
public sealed record GetCustomersQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null)
    : IRequest<Result<PaginatedList<CustomerDto>>>;
