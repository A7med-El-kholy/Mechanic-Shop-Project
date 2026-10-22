using FluentValidation;

namespace MechanicShop.Application.Features.ServiceTasks.Commands.UpdateServiceTask;

public sealed class UpdateServiceTaskCommandValidator : AbstractValidator<UpdateServiceTaskCommand>
{
    public UpdateServiceTaskCommandValidator()
    {
        RuleFor(x => x.ServiceTaskId)
            .NotEmpty().WithMessage("Service task id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service name is required.")
            .MaximumLength(150);

        RuleFor(x => x.BasePrice)
            .InclusiveBetween(0.01m, 10000m)
            .WithMessage("Base price must be greater than 0 and at most 10000.");

        RuleFor(x => x.EstimatedDurationInMins)
            .InclusiveBetween(15, 180).WithMessage("Estimated duration must be between 15 and 180 minutes.")
            .Must(mins => mins % 15 == 0).WithMessage("Estimated duration must be a 15-minute step.");
    }
}
