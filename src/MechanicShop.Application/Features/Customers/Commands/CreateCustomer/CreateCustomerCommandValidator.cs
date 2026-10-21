using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email.")
            .MaximumLength(150);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?\d{7,15}$").WithMessage("Phone number must be 7-15 digits and may start with '+'.");

        RuleFor(x => x.Vehicles)
            .NotNull().WithMessage("Vehicle list cannot be null.")
            .NotEmpty().WithMessage("At least one vehicle is required.");

        RuleForEach(x => x.Vehicles).SetValidator(new CreateVehiclePayloadValidator());
    }
}

public sealed class CreateVehiclePayloadValidator : AbstractValidator<CreateVehiclePayload>
{
    public CreateVehiclePayloadValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().WithMessage("Vehicle make is required.")
            .MaximumLength(100);

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Vehicle model is required.")
            .MaximumLength(100);

        RuleFor(x => x.Year)
            .InclusiveBetween(1886, 2100).WithMessage("Year must be between 1886 and next year.");

        RuleFor(x => x.LicensePlate)
            .NotEmpty().WithMessage("License plate is required.")
            .MaximumLength(20);

        RuleFor(x => x.Vin)
            .NotEmpty().WithMessage("VIN is required.")
            .Matches("^[A-HJ-NPR-Z0-9]{17}$")
            .WithMessage("VIN must be exactly 17 characters, alphanumeric excluding I, O and Q.");
    }
}
