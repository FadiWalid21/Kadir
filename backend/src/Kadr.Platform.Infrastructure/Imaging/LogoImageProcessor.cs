using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using SkiaSharp;

namespace Kadr.Platform.Infrastructure.Imaging;

public sealed class LogoImageProcessor : IImageProcessor
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxEdge = 512;

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    public Task<ProcessedImage> ProcessLogoAsync(
        Stream content,
        string? declaredContentType,
        string? originalFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        if (buffer.Length == 0)
        {
            throw new InvalidImageException(ErrorCodes.FileInvalidType, "Empty file.");
        }

        if (buffer.Length > MaxBytes)
        {
            throw new InvalidImageException(ErrorCodes.FileTooLarge, "File exceeds 2 MB.");
        }

        var bytes = buffer.ToArray();
        if (IsPng(bytes) || IsJpeg(bytes))
        {
            return Task.FromResult(ProcessRaster(bytes));
        }

        if (LooksLikeSvg(bytes, declaredContentType, originalFileName))
        {
            return Task.FromResult(ProcessSvg(bytes));
        }

        throw new InvalidImageException(ErrorCodes.FileInvalidType, "Unsupported image type.");
    }

    private static ProcessedImage ProcessRaster(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
        {
            throw new InvalidImageException(ErrorCodes.FileInvalidType, "Unsupported image type.");
        }

        SKBitmap working = bitmap;
        SKBitmap? resized = null;
        try
        {
            if (bitmap.Width > MaxEdge || bitmap.Height > MaxEdge)
            {
                var scale = Math.Min((float)MaxEdge / bitmap.Width, (float)MaxEdge / bitmap.Height);
                var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
                var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
                resized = bitmap.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKFilterMode.Linear));
                if (resized is null)
                {
                    throw new InvalidImageException(ErrorCodes.FileInvalidType, "Could not resize image.");
                }

                working = resized;
            }

            using var image = SKImage.FromBitmap(working);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            if (data is null)
            {
                throw new InvalidImageException(ErrorCodes.FileInvalidType, "Could not encode image.");
            }

            var encoded = data.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant();
            return new ProcessedImage(new MemoryStream(encoded), "image/png", "logo.png", hash);
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static ProcessedImage ProcessSvg(byte[] bytes)
    {
        string text;
        try
        {
            text = Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidImageException(ErrorCodes.FileInvalidType, "Invalid SVG encoding.");
        }

        if (text.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || text.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("onload=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("xlink:href=\"http", StringComparison.OrdinalIgnoreCase)
            || text.Contains("href=\"http", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidImageException(ErrorCodes.FileInvalidType, "SVG contains disallowed content.");
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
            };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            var foundSvg = false;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element
                    && reader.Name.Equals("svg", StringComparison.OrdinalIgnoreCase))
                {
                    foundSvg = true;
                    break;
                }
            }

            if (!foundSvg)
            {
                throw new InvalidImageException(ErrorCodes.FileInvalidType, "Not a valid SVG.");
            }
        }
        catch (XmlException)
        {
            throw new InvalidImageException(ErrorCodes.FileInvalidType, "Not a valid SVG.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ProcessedImage(new MemoryStream(bytes), "image/svg+xml", "logo.svg", hash);
    }

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= PngMagic.Length && bytes[..PngMagic.Length].SequenceEqual(PngMagic);

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= JpegMagic.Length && bytes[..JpegMagic.Length].SequenceEqual(JpegMagic);

    private static bool LooksLikeSvg(byte[] bytes, string? contentType, string? fileName)
    {
        if (string.Equals(contentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var head = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256)));
        return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }
}
