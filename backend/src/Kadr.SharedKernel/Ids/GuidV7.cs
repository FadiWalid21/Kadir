namespace Kadr.SharedKernel.Ids;

public static class GuidV7
{
    public static Guid NewGuid() => Guid.CreateVersion7();
}
