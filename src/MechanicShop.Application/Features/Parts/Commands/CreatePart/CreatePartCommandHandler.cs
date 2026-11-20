using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Application.Features.Parts.Mappers;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Parts.Commands.CreatePart;

public sealed class CreatePartCommandHandler(IAppDbContext context, IUser user, ILogger<CreatePartCommandHandler> logger)
    : IRequestHandler<CreatePartCommand, Result<PartDto>>
{
    private readonly IAppDbContext _context = context;
    private readonly ILogger<CreatePartCommandHandler> _logger = logger;
    private readonly IUser _user = user;

    public async Task<Result<PartDto>> Handle(CreatePartCommand command, CancellationToken cancellationToken)
    {
        if (Enum.IsDefined(typeof(PartCategory), command.Category) is false)
        {
            return PartErrors.CategoryInvalid;
        }

        var normalizedName = command.Name.Trim().ToLowerInvariant();

        var nameTaken = await _context.Parts
            .AnyAsync(p => p.Name.ToLower() == normalizedName, cancellationToken);

        if (nameTaken)
        {
            return PartErrors.AlreadyExists(command.Name.Trim());
        }

        var created = Part.Create(
            Guid.NewGuid(),
            command.Name,
            command.Category,
            command.Price,
            command.Quantity,
            command.MinimumQuantity);

        if (created.IsError)
        {
            return created.Errors;
        }

        _context.Parts.Add(created.Value);
        if (created.Value.Quantity > 0)
        {
            _context.StockMovements.Add(StockMovement.Create(created.Value.Id, created.Value.Quantity,
                0, created.Value.Quantity, "Initial stock on part creation", _user.UserId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Part {PartId} '{PartName}' stocked in category {Category}.",
            created.Value.Id,
            created.Value.Name,
            created.Value.Category);

        return created.Value.ToDto();
    }
}
