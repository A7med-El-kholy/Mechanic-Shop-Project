using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle id is required.");

        RuleFor(x => x.ServiceTaskIds)
            .NotEmpty().WithMessage("A work order must include at least one service task.");

        RuleForEach(x => x.ServiceTaskIds)
            .NotEmpty().WithMessage("Service task ids must not be empty values.");
    }
}
