using FluentValidation;

namespace MechanicShop.Application.Features.Vehicles.Commands.RestoreVehicle;

public sealed class RestoreVehicleCommandValidator : AbstractValidator<RestoreVehicleCommand>
{
    public RestoreVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle id is required.");
    }
}
