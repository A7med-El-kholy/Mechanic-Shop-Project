using MechanicShop.Application.Features.Invoices.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Invoices.Queries.GetMyInvoiceById;

public sealed record GetMyInvoiceByIdQuery(Guid InvoiceId)
    : IRequest<Result<InvoiceDto>>;
