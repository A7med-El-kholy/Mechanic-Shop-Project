using FluentValidation;

namespace MechanicShop.Application.Features.Vehicles.Commands.RemoveVehicle;

public sealed class RemoveVehicleCommandValidator : AbstractValidator<RemoveVehicleCommand>
{
    public RemoveVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle id is required.");
    }
}
