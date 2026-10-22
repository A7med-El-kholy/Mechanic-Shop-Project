using FluentValidation;

namespace MechanicShop.Application.Features.Customers.Commands.AddVehicleToCustomer;

public sealed class AddVehicleToCustomerCommandValidator : AbstractValidator<AddVehicleToCustomerCommand>
{
    public AddVehicleToCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer id is required.");

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
