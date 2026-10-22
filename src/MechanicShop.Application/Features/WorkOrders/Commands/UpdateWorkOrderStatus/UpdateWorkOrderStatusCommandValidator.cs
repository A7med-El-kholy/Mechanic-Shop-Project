using FluentValidation;

using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderStatus;

public sealed class UpdateWorkOrderStatusCommandValidator : AbstractValidator<UpdateWorkOrderStatusCommand>
{
    public UpdateWorkOrderStatusCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order id is required.");

        RuleFor(x => x.State)
            .Must(value => Enum.IsDefined(typeof(WorkOrderState), value))
            .WithMessage("State must be one of: Scheduled (0), InProgress (1), Completed (2), Cancelled (3).");
    }
}
