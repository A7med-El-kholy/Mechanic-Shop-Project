using FluentValidation;

namespace MechanicShop.Application.Features.ServiceTasks.Queries.GetServiceTaskById;

public sealed class GetServiceTaskByIdQueryValidator : AbstractValidator<GetServiceTaskByIdQuery>
{
    public GetServiceTaskByIdQueryValidator()
    {
        RuleFor(x => x.ServiceTaskId)
            .NotEmpty().WithMessage("Service task id is required.");
    }
}
