using FluentValidation;

namespace MechanicShop.Application.Features.Vehicles.Commands.ArchiveVehicle;

public sealed class ArchiveVehicleCommandValidator : AbstractValidator<ArchiveVehicleCommand>
{
    public ArchiveVehicleCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle id is required.");
    }
}
