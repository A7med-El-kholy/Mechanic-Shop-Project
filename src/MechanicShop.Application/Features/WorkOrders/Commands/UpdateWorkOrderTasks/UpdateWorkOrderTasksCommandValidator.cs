using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderTasks;

public sealed class UpdateWorkOrderTasksCommandValidator : AbstractValidator<UpdateWorkOrderTasksCommand>
{
    public UpdateWorkOrderTasksCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order id is required.");

        RuleFor(x => x.ServiceTaskIds)
            .NotEmpty().WithMessage("A work order must include at least one service task.");

        RuleForEach(x => x.ServiceTaskIds)
            .NotEmpty().WithMessage("Service task ids must not be empty values.");
    }
}
