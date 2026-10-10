using Kadr.Platform.Infrastructure.Imaging;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using SkiaSharp;
using Xunit;

namespace Kadr.Platform.Tests.Imaging;

public sealed class LogoImageProcessorTests
{
    private readonly LogoImageProcessor _sut = new();

    [Fact]
    public async Task Accepts_png_and_returns_png()
    {
        await using var input = new MemoryStream(CreatePng(64, 64));
        var result = await _sut.ProcessLogoAsync(input, "image/png", "x.png");
        await using (result.Content)
        {
            Assert.Equal("image/png", result.ContentType);
            Assert.True(result.Content.Length > 0);
            Assert.False(string.IsNullOrWhiteSpace(result.Sha256Hex));
        }
    }

    [Fact]
    public async Task Rejects_oversized_file()
    {
        var huge = new byte[LogoImageProcessor.MaxBytes + 1];
        PngMagic.CopyTo(huge, 0);
        await using var input = new MemoryStream(huge);
        var ex = await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.ProcessLogoAsync(input, "image/png", "big.png"));
        Assert.Equal(ErrorCodes.FileTooLarge, ex.ErrorCode);
    }

    [Fact]
    public async Task Rejects_non_image_bytes()
    {
        await using var input = new MemoryStream("not-an-image"u8.ToArray());
        var ex = await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.ProcessLogoAsync(input, "application/octet-stream", "x.bin"));
        Assert.Equal(ErrorCodes.FileInvalidType, ex.ErrorCode);
    }

    [Fact]
    public async Task Rejects_svg_with_script()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>"""u8.ToArray();
        await using var input = new MemoryStream(svg);
        var ex = await Assert.ThrowsAsync<InvalidImageException>(() =>
            _sut.ProcessLogoAsync(input, "image/svg+xml", "x.svg"));
        Assert.Equal(ErrorCodes.FileInvalidType, ex.ErrorCode);
    }

    [Fact]
    public async Task Accepts_clean_svg()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>"""u8.ToArray();
        await using var input = new MemoryStream(svg);
        var result = await _sut.ProcessLogoAsync(input, "image/svg+xml", "x.svg");
        await using (result.Content)
        {
            Assert.Equal("image/svg+xml", result.ContentType);
        }
    }

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
