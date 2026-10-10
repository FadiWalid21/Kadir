using Kadr.SharedKernel.Ids;

namespace Kadr.SharedKernel.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; } = GuidV7.NewGuid();
}
