using System.Globalization;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Settings;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class OvertimeMultipliersService(
    KadrDbContext db,
    ITenantContext tenantContext) : IOvertimeMultipliersService
{
    private static readonly decimal DefaultDay = 1.35m;
    private static readonly decimal DefaultNight = 1.70m;
    private static readonly decimal DefaultHoliday = 2.00m;

    public async Task<Result<OvertimeMultipliersDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        var tenantId = tenantContext.TenantId.Value;
        var keys = new[]
        {
            SettingKeys.OvertimeMultiplierDay,
            SettingKeys.OvertimeMultiplierNight,
            SettingKeys.OvertimeMultiplierHoliday,
        };

        var existing = await db.TenantSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byKey = existing.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var changed = false;

        changed |= Ensure(byKey, tenantId, SettingKeys.OvertimeMultiplierDay, DefaultDay);
        changed |= Ensure(byKey, tenantId, SettingKeys.OvertimeMultiplierNight, DefaultNight);
        changed |= Ensure(byKey, tenantId, SettingKeys.OvertimeMultiplierHoliday, DefaultHoliday);

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new OvertimeMultipliersDto(
            Parse(byKey[SettingKeys.OvertimeMultiplierDay].ValueJson, DefaultDay),
            Parse(byKey[SettingKeys.OvertimeMultiplierNight].ValueJson, DefaultNight),
            Parse(byKey[SettingKeys.OvertimeMultiplierHoliday].ValueJson, DefaultHoliday)));
    }

    private bool Ensure(
        Dictionary<string, TenantSetting> byKey,
        Guid tenantId,
        string key,
        decimal defaultValue)
    {
        if (byKey.ContainsKey(key))
        {
            return false;
        }

        var setting = TenantSetting.Create(tenantId, key, JsonSerializer.Serialize(defaultValue));
        db.TenantSettings.Add(setting);
        byKey[key] = setting;
        return true;
    }

    private static decimal Parse(string valueJson, decimal fallback)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<decimal>(valueJson);
            return parsed > 0 ? parsed : fallback;
        }
        catch (JsonException)
        {
            if (decimal.TryParse(
                    valueJson.Trim('"'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var n)
                && n > 0)
            {
                return n;
            }

            return fallback;
        }
    }
}
