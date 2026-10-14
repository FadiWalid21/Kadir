using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Subscriptions;
using Kadr.TenantIsolation.Tests.Register;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Subscriptions;

[Collection(RegisterCollection.Name)]
public sealed class PublicPlansApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Anonymous_list_returns_active_plans()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/public/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plans = await response.Content.ReadFromJsonAsync<List<PlanCatalogItemDto>>(JsonOptions);
        Assert.NotNull(plans);
        Assert.NotEmpty(plans!);
    }
}
