using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Files;

public sealed class FileObject : AggregateRoot, ITenantOwned, IAuditable
{
    private FileObject()
    {
    }

    public Guid TenantId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Hash { get; private set; } = string.Empty;
    public FilePurpose Purpose { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static FileObject Create(
        Guid id,
        Guid tenantId,
        string storagePath,
        string fileName,
        string contentType,
        long sizeBytes,
        string hash,
        FilePurpose purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new FileObject
        {
            Id = id,
            TenantId = tenantId,
            StoragePath = storagePath.Trim(),
            FileName = fileName.Trim(),
            ContentType = contentType.Trim().ToLowerInvariant(),
            SizeBytes = sizeBytes,
            Hash = hash.Trim().ToLowerInvariant(),
            Purpose = purpose,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
