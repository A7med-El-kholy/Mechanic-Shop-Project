using FluentValidation;

namespace MechanicShop.Application.Features.Employees.Commands.RemoveEmployee;

public sealed class RemoveEmployeeCommandValidator : AbstractValidator<RemoveEmployeeCommand>
{
    public RemoveEmployeeCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("Employee id is required.");
    }
}
