using MechanicShop.Application.Features.WorkOrders.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.WorkOrders.Queries.GetMyRepairById;

/// <summary>Repair detail for the owner from the token. A customer cannot read a
/// work order by guessing ids: any job not theirs reads as not found.</summary>
public sealed record GetMyRepairByIdQuery(Guid WorkOrderId)
    : IRequest<Result<WorkOrderDto>>;
