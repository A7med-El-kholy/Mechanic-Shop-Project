using FluentValidation;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.RemoveServiceTask;

public sealed class RemoveServiceTaskCommandValidator : AbstractValidator<RemoveServiceTaskCommand>
{
    public RemoveServiceTaskCommandValidator()
    {
        RuleFor(x => x.ServiceTaskId)
            .NotEmpty().WithMessage("Service task id is required.");
    }
}
