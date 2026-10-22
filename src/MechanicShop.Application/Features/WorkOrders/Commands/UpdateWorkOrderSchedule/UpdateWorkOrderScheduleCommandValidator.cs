using FluentValidation;

using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateWorkOrderSchedule;

public sealed class UpdateWorkOrderScheduleCommandValidator : AbstractValidator<UpdateWorkOrderScheduleCommand>
{
    public UpdateWorkOrderScheduleCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty().WithMessage("Work order id is required.");

        RuleFor(x => x.ScheduledDate)
            .NotEqual(default(DateOnly)).WithMessage("Scheduled date is required.");

        RuleFor(x => x.StartsAtMinute)
            .Must(WorkDay.IsGridSlot)
            .WithMessage("Start time must be a 15-minute slot between 08:00 and 19:45.");

        RuleFor(x => x.Spot)
            .InclusiveBetween(0, 2)
            .WithMessage("Spot must be one of: A (0), B (1), C (2).");

        RuleFor(x => x.TechnicianId)
            .NotEmpty().WithMessage("Technician id is required.");
    }
}
