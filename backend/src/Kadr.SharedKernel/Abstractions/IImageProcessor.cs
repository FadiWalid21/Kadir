namespace Kadr.SharedKernel.Abstractions;

public sealed record ProcessedImage(
    Stream Content,
    string ContentType,
    string SuggestedFileName,
    string Sha256Hex);

public interface IImageProcessor
{
    /// <summary>
    /// Validates content (magic bytes / SVG rules), enforces max size, optimizes rasters.
    /// Returns Failure via exception codes through Result in the application layer —
    /// this method throws <see cref="InvalidImageException"/> on rejection.
    /// </summary>
    Task<ProcessedImage> ProcessLogoAsync(
        Stream content,
        string? declaredContentType,
        string? originalFileName,
        CancellationToken cancellationToken = default);
}

public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
