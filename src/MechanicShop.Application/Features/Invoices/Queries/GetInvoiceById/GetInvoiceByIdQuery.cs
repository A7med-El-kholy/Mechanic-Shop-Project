using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId)
    : IRequest<Result<InvoiceDto>>;
