using FluentValidation;

using MechanicShop.Domain.Employees.Enums;

namespace MechanicShop.Application.Features.Employees.Commands.CreateEmployee;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Employee name is required.")
            .MaximumLength(100).WithMessage("Employee name must be at most 100 characters.");

        RuleFor(x => x.Role)
            .Must(value => value == (int)EmployeeRole.Technician || value == (int)EmployeeRole.Manager)
            .WithMessage("Role must be one of: Technician (0), Manager (1).");
    }
}
