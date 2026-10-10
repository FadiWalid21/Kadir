using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Testing.Support.Notes;

/// <summary>
/// Isolation-harness aggregate. Registered only on <see cref="Persistence.TestKadrDbContext"/>.
/// </summary>
public sealed class TenantNote : AggregateRoot, ITenantOwned, IAuditable
{
    private TenantNote()
    {
    }

    public Guid TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static TenantNote Create(Guid tenantId, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var note = new TenantNote
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        note.Raise(new TenantNoteCreated(note.Id, note.TenantId, note.Title));
        return note;
    }

    public void Rename(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
    }
}

public sealed record TenantNoteCreated(Guid NoteId, Guid TenantId, string Title) : IDomainEvent;
