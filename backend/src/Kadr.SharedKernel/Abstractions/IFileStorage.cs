namespace Kadr.SharedKernel.Abstractions;

public enum FilePurpose
{
    Logo = 0,
    Document = 1,
    Photo = 2,
    Temp = 3,
}

public sealed record StoredFile(
    Guid FileId,
    string StoragePath,
    string FileName,
    string ContentType,
    long SizeBytes);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Guid tenantId,
        FilePurpose purpose,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Uri> GetTemporaryReadUrlAsync(
        Guid tenantId,
        Guid fileId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid fileId, CancellationToken cancellationToken = default);
}
