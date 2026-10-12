using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class OvertimeMultipliersService(
    KadrDbContext db,
    ITenantContext tenantContext) : IOvertimeMultipliersService
{
    public async Task<Result<OvertimeMultipliersDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        var legalEntityId = await db.LegalEntities.AsNoTracking()
            .Where(x => x.IsDefault)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (legalEntityId is null)
        {
            return Result.Success(new OvertimeMultipliersDto(0m, 0m, 0m));
        }

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var keys = new[]
        {
            PayrollRuleKeys.OvertimeMultiplierDay,
            PayrollRuleKeys.OvertimeMultiplierNight,
            PayrollRuleKeys.OvertimeMultiplierHoliday,
        };

        var rows = await db.TenantRuleValues.AsNoTracking()
            .Where(x => x.LegalEntityId == legalEntityId && keys.Contains(x.RuleKey) && x.EffectiveFrom <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = rows
            .GroupBy(x => x.RuleKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).First(),
                StringComparer.Ordinal);

        return Result.Success(new OvertimeMultipliersDto(
            ReadMultiplier(current, PayrollRuleKeys.OvertimeMultiplierDay),
            ReadMultiplier(current, PayrollRuleKeys.OvertimeMultiplierNight),
            ReadMultiplier(current, PayrollRuleKeys.OvertimeMultiplierHoliday)));
    }

    private static decimal ReadMultiplier(Dictionary<string, TenantRuleValue> current, string key)
    {
        if (!current.TryGetValue(key, out var row))
        {
            return 0m;
        }

        try
        {
            using var doc = JsonDocument.Parse(row.ValueJson);
            if (!doc.RootElement.TryGetProperty("multiplier", out var el)
                || el.ValueKind == JsonValueKind.Null
                || el.ValueKind != JsonValueKind.Number)
            {
                return 0m;
            }

            return el.GetDecimal();
        }
        catch (JsonException)
        {
            return 0m;
        }
    }
}
