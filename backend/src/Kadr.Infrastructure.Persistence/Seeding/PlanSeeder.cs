using Kadr.Platform.Domain.Plans;
using Kadr.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Seeding;

public sealed class PlanSeeder(KadrDbContext db, ILogger<PlanSeeder> logger)
{
    private static readonly string StarterFeatures =
        """{"attendance":true,"leave":true,"ess":true,"payroll":false}""";

    private static readonly string GrowthFeatures =
        """{"attendance":true,"leave":true,"ess":true,"shifts":true,"workflows":true,"reports":true,"documents":true,"payroll":false}""";

    private static readonly string BusinessFeatures =
        """{"attendance":true,"leave":true,"ess":true,"shifts":true,"workflows":true,"reports":true,"documents":true,"custom_fields":true,"api":true,"payroll":false}""";

    private static readonly string CoreFeatures =
        """{"attendance":true,"leave":true,"ess":true,"payroll":false}""";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Plans.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        db.Plans.AddRange(
            Plan.CreateFlat(PlanCodes.Starter, "المبتدئ", "Starter", 25, StarterFeatures),
            Plan.CreateFlat(PlanCodes.Growth, "النمو", "Growth", 100, GrowthFeatures),
            Plan.CreateFlat(PlanCodes.Business, "الأعمال", "Business", 300, BusinessFeatures),
            Plan.CreatePerEmployee(PlanCodes.Core, "الأساسي", "Core", 0m, 10, CoreFeatures));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded platform plans");
    }
}
