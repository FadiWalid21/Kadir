namespace Kadr.SharedKernel.Domain;

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; protected set; }
    public Guid? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected void TouchCreated(DateTimeOffset? at = null)
    {
        CreatedAt = at ?? DateTimeOffset.UtcNow;
    }

    protected void TouchUpdated(DateTimeOffset? at = null)
    {
        UpdatedAt = at ?? DateTimeOffset.UtcNow;
    }
}
