using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Ids;
using Xunit;

namespace Kadr.Platform.Tests.Domain;

public sealed class TenantNoteTests
{
    [Fact]
    public void Create_raises_TenantNoteCreated_and_sets_fields()
    {
        var tenantId = GuidV7.NewGuid();
        var note = TenantNote.Create(tenantId, "  Hello  ");

        Assert.Equal(tenantId, note.TenantId);
        Assert.Equal("Hello", note.Title);
        Assert.Contains(note.DomainEvents, e => e is TenantNoteCreated created
            && created.NoteId == note.Id
            && created.TenantId == tenantId
            && created.Title == "Hello");
    }
}
