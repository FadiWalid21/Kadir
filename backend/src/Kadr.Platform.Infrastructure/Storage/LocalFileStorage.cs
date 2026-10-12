using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Ids;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Infrastructure.Storage;

public sealed class LocalFileStorage(ILogger<LocalFileStorage> logger) : IFileStorage
{
    private readonly string _root = Path.Combine(Directory.GetCurrentDirectory(), "storage");

    public async Task<StoredFile> SaveAsync(
        Guid tenantId,
        FilePurpose purpose,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var fileId = GuidV7.NewGuid();
        var extension = ResolveExtension(contentType, fileName);
        var storedName = $"{fileId:N}{extension}";
        var relativeDir = Path.Combine(
            "tenants",
            tenantId.ToString("N"),
            purpose.ToString().ToLowerInvariant());
        var directory = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(directory);

        var relativePath = Path.Combine(relativeDir, storedName);
        var fullPath = Path.Combine(_root, relativePath);

        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        var size = new FileInfo(fullPath).Length;
        logger.LogInformation("Stored file {FileId} for tenant {TenantId}", fileId, tenantId);

        return new StoredFile(
            fileId,
            relativePath.Replace('\\', '/'),
            storedName,
            contentType,
            size);
    }

    public Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSafePath(storagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file was not found.", fullPath);
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveSafePath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            logger.LogInformation("Deleted stored file at {StoragePath}", storagePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        var normalized = storagePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(storagePath))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootFull = Path.GetFullPath(_root);
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }

    private static string ResolveExtension(string contentType, string fileName)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/svg+xml" => ".svg",
            _ => Path.GetExtension(fileName) is { Length: > 0 } ext ? ext.ToLowerInvariant() : ".bin",
        };
    }
}
