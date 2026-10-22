using FluentValidation;

namespace MechanicShop.Application.Features.Vehicles.Queries.GetVehicleById;

public sealed class GetVehicleByIdQueryValidator : AbstractValidator<GetVehicleByIdQuery>
{
    public GetVehicleByIdQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty().WithMessage("Vehicle id is required.");
    }
}
