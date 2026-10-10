using Kadr.Platform.Application.Tenants;
using Xunit;

namespace Kadr.Platform.Tests.Tenants;

public sealed class EmailNormalizerTests
{
    [Fact]
    public void Normalize_trims_and_uppercases_ForDisplay_lowercases()
    {
        Assert.Equal("OWNER@EXAMPLE.COM", EmailNormalizer.Normalize("  Owner@Example.com "));
        Assert.Equal("owner@example.com", EmailNormalizer.ForDisplay("  Owner@Example.com "));
    }
}
