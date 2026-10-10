using Kadr.Platform.Application.Tenants;
using Xunit;

namespace Kadr.Platform.Tests.Tenants;

public sealed class TenantSlugTests
{
    [Fact]
    public void FromName_slugifies_and_WithSuffix_resolves_collisions()
    {
        Assert.Equal("acme-corp", TenantSlug.FromName("  Acme Corp  "));
        Assert.Equal("api-co", TenantSlug.FromName("API"));

        var baseSlug = "acme";
        var first = TenantSlug.WithSuffix(baseSlug, Guid.Parse("00000000-0000-7000-0000-000000000001"));
        var second = TenantSlug.WithSuffix(baseSlug, Guid.Parse("ffffffff-ffff-7fff-ffff-ffffffffffff"));

        Assert.StartsWith("acme-", first);
        Assert.StartsWith("acme-", second);
        Assert.Equal(9, first.Length);
        Assert.Equal(9, second.Length);
        Assert.NotEqual(first, second);
    }
}
