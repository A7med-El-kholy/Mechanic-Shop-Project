namespace MechanicShop.Domain.Common;

/// <summary>
/// Base class for entities with a caller-supplied or generated identity.
/// Domain events (added later, when notification features need them) will hang off here too.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; }

    protected Entity()
    { }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }
}
