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

    /// <summary>Opens a stored file by relative storage path for authenticated streaming.</summary>
    Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
