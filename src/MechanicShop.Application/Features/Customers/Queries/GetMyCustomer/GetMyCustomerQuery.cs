using MechanicShop.Application.Features.Customers.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Customers.Queries.GetMyCustomer;

/// <summary>
/// The caller's own customer profile, resolved from the identity claims — no client-
/// supplied id exists, so a customer can never read someone else's record.
/// </summary>
public sealed record GetMyCustomerQuery()
    : IRequest<Result<CustomerDto>>;
