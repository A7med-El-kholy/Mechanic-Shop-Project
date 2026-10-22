using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.RemoveWorkOrder;

public sealed class RemoveWorkOrderCommandValidator : AbstractValidator<RemoveWorkOrderCommand>
{
    public RemoveWorkOrderCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order id is required.");
    }
}
