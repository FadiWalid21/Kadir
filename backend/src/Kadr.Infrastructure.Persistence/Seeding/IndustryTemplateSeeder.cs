using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Seeding;

public sealed class IndustryTemplateSeeder(
    KadrDbContext db,
    IIndustryTemplateSeedSource seedSource,
    ILogger<IndustryTemplateSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var documents = seedSource.LoadAll();
        var changed = 0;

        foreach (var doc in documents)
        {
            var payloadJson = JsonSerializer.Serialize(doc, JsonOptions);
            var existing = await db.IndustryTemplates
                .SingleOrDefaultAsync(t => t.Code == doc.Code && t.Version == doc.Version, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                db.IndustryTemplates.Add(IndustryTemplate.Create(
                    doc.Code,
                    doc.Version,
                    doc.NameAr,
                    doc.NameEn,
                    payloadJson,
                    doc.DescriptionAr,
                    doc.DescriptionEn));
                changed++;
            }
            else if (!string.Equals(existing.PayloadJson, payloadJson, StringComparison.Ordinal)
                     || !string.Equals(existing.NameAr, doc.NameAr, StringComparison.Ordinal)
                     || !string.Equals(existing.NameEn, doc.NameEn, StringComparison.Ordinal))
            {
                existing.ReplacePayload(
                    doc.NameAr,
                    doc.NameEn,
                    payloadJson,
                    doc.DescriptionAr,
                    doc.DescriptionEn);
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Upserted {Count} industry template seed rows", changed);
        }
    }
}
