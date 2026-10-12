using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.Platform.Domain.Settings;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class PayrollRulesService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit) : IPayrollRulesService
{
    public async Task<Result<PayrollRulesListDto>> ListCurrentAsync(CancellationToken cancellationToken = default)
    {
        var legalEntityId = await GetDefaultLegalEntityIdAsync(cancellationToken).ConfigureAwait(false);
        if (legalEntityId is null)
        {
            return Result.Failure<PayrollRulesListDto>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await LoadCurrentRowsAsync(legalEntityId.Value, asOf, cancellationToken)
            .ConfigureAwait(false);
        var platform = await LoadLatestPlatformAsync(cancellationToken).ConfigureAwait(false);
        var confirmed = await ReadConfirmationAsync(cancellationToken).ConfigureAwait(false);

        var items = PayrollRuleKeys.All.Select(key =>
        {
            current.TryGetValue(key, out var row);
            platform.TryGetValue(key, out var official);
            var valueJson = row?.ValueJson ?? official?.ValueJson ?? "{}";
            var officialJson = official?.ValueJson;
            var differs = row is not null
                && officialJson is not null
                && !string.Equals(row.ValueJson, officialJson, StringComparison.Ordinal);
            var updateAvailable = row is not null
                && official is not null
                && official.Version > (row.SourceVersion ?? 0);

            return new PayrollRuleListItemDto(
                key,
                row?.ValueType ?? official?.ValueType ?? PayrollRuleValueTypes.Enum,
                valueJson,
                row?.EffectiveFrom ?? official?.EffectiveFrom ?? asOf,
                row?.IsCustomized ?? false,
                row?.IsVerified ?? official?.IsVerified ?? false,
                differs,
                updateAvailable,
                officialJson,
                row?.Reason ?? string.Empty,
                row?.Id ?? Guid.Empty);
        }).ToList();

        return Result.Success(new PayrollRulesListDto(
            items,
            confirmed.Confirmed,
            confirmed.At,
            confirmed.Reason));
    }

    public async Task<Result<IReadOnlyList<PayrollRuleHistoryItemDto>>> GetHistoryAsync(
        string ruleKey,
        CancellationToken cancellationToken = default)
    {
        if (!PayrollRuleKeyGuard.IsKnown(ruleKey))
        {
            return Result.Failure<IReadOnlyList<PayrollRuleHistoryItemDto>>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var legalEntityId = await GetDefaultLegalEntityIdAsync(cancellationToken).ConfigureAwait(false);
        if (legalEntityId is null)
        {
            return Result.Failure<IReadOnlyList<PayrollRuleHistoryItemDto>>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var rows = await db.TenantRuleValues.AsNoTracking()
            .Where(x => x.LegalEntityId == legalEntityId && x.RuleKey == ruleKey)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new PayrollRuleHistoryItemDto(
                x.Id,
                x.ValueJson,
                x.EffectiveFrom,
                x.IsCustomized,
                x.IsVerified,
                x.Reason,
                x.CreatedAt,
                x.CreatedBy))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<PayrollRuleHistoryItemDto>>(rows);
    }

    public async Task<Result<PayrollRuleListItemDto>> UpsertAsync(
        string ruleKey,
        UpsertPayrollRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!PayrollRuleKeyGuard.IsKnown(ruleKey))
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var validation = ValidateValueJson(ruleKey, request.ValueJson);
        if (validation is not null)
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesValidationFailed,
                validation);
        }

        var legalEntityId = await GetDefaultLegalEntityIdAsync(cancellationToken).ConfigureAwait(false);
        if (legalEntityId is null || tenantContext.TenantId is null)
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var platform = await db.StatutoryRuleSets.AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country && x.RuleKey == ruleKey)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var valueType = platform?.ValueType ?? InferValueType(ruleKey);
        var entity = TenantRuleValue.CreateCustom(
            tenantContext.TenantId.Value,
            legalEntityId.Value,
            ruleKey,
            valueType,
            request.ValueJson,
            request.EffectiveFrom,
            request.Reason,
            tenantContext.UserId,
            platform?.Id,
            platform?.Version,
            isVerified: false);

        db.TenantRuleValues.Add(entity);
        await ClearConfirmationAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            "payroll.rules.upsert",
            "TenantRuleValue",
            entity.Id,
            null,
            JsonSerializer.Serialize(new { ruleKey, request.EffectiveFrom, request.Reason }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(await MapCurrentItemAsync(legalEntityId.Value, ruleKey, cancellationToken)
            .ConfigureAwait(false));
    }

    public async Task<Result<PayrollRuleListItemDto>> ResetAsync(
        string ruleKey,
        string reason,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken = default)
    {
        if (!PayrollRuleKeyGuard.IsKnown(ruleKey) || string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesValidationFailed,
                ErrorCodes.PayrollRulesValidationFailed);
        }

        var platform = await db.StatutoryRuleSets.AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country && x.RuleKey == ruleKey)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (platform is null)
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var legalEntityId = await GetDefaultLegalEntityIdAsync(cancellationToken).ConfigureAwait(false);
        if (legalEntityId is null || tenantContext.TenantId is null)
        {
            return Result.Failure<PayrollRuleListItemDto>(
                ErrorCodes.PayrollRulesNotFound,
                ErrorCodes.PayrollRulesNotFound);
        }

        var entity = TenantRuleValue.CreateReset(
            tenantContext.TenantId.Value,
            legalEntityId.Value,
            platform,
            effectiveFrom,
            reason.Trim(),
            tenantContext.UserId);

        db.TenantRuleValues.Add(entity);
        await ClearConfirmationAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            "payroll.rules.reset",
            "TenantRuleValue",
            entity.Id,
            null,
            JsonSerializer.Serialize(new { ruleKey, effectiveFrom, reason }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(await MapCurrentItemAsync(legalEntityId.Value, ruleKey, cancellationToken)
            .ConfigureAwait(false));
    }

    public async Task<Result<ConfirmPayrollRulesDto>> ConfirmForLockAsync(
        ConfirmPayrollRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<ConfirmPayrollRulesDto>(
                ErrorCodes.PayrollRulesForbidden,
                ErrorCodes.PayrollRulesForbidden);
        }

        var payload = JsonSerializer.Serialize(new
        {
            confirmed = true,
            at = DateTimeOffset.UtcNow,
            reason = request.Reason.Trim(),
            by = tenantContext.UserId,
        });

        var setting = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.Key == SettingKeys.PayrollRulesConfirmed, cancellationToken)
            .ConfigureAwait(false);
        if (setting is null)
        {
            setting = TenantSetting.Create(tenantContext.TenantId.Value, SettingKeys.PayrollRulesConfirmed, payload);
            db.TenantSettings.Add(setting);
        }
        else
        {
            setting.SetValue(payload);
        }

        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            "payroll.rules.confirm",
            "TenantSetting",
            setting.Id,
            null,
            JsonSerializer.Serialize(new { request.Reason }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var confirmed = await ReadConfirmationAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ConfirmPayrollRulesDto(
            confirmed.Confirmed,
            confirmed.At ?? DateTimeOffset.UtcNow,
            confirmed.Reason ?? request.Reason));
    }

    public async Task<bool> AreRulesConfirmedForLockAsync(CancellationToken cancellationToken = default)
    {
        var confirmed = await ReadConfirmationAsync(cancellationToken).ConfigureAwait(false);
        return confirmed.Confirmed;
    }

    private async Task<PayrollRuleListItemDto> MapCurrentItemAsync(
        Guid legalEntityId,
        string ruleKey,
        CancellationToken cancellationToken)
    {
        var list = await ListCurrentAsync(cancellationToken).ConfigureAwait(false);
        return list.Value!.Items.First(x => x.RuleKey == ruleKey);
    }

    private async Task<Guid?> GetDefaultLegalEntityIdAsync(CancellationToken cancellationToken) =>
        await db.LegalEntities.AsNoTracking()
            .Where(x => x.IsDefault)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<Dictionary<string, TenantRuleValue>> LoadCurrentRowsAsync(
        Guid legalEntityId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var rows = await db.TenantRuleValues.AsNoTracking()
            .Where(x => x.LegalEntityId == legalEntityId && x.EffectiveFrom <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(x => x.RuleKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).First(),
                StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, StatutoryRuleSet>> LoadLatestPlatformAsync(
        CancellationToken cancellationToken)
    {
        var rows = await db.StatutoryRuleSets.AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(x => x.RuleKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Version).First(),
                StringComparer.Ordinal);
    }

    private async Task<(bool Confirmed, DateTimeOffset? At, string? Reason)> ReadConfirmationAsync(
        CancellationToken cancellationToken)
    {
        var setting = await db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == SettingKeys.PayrollRulesConfirmed, cancellationToken)
            .ConfigureAwait(false);
        if (setting is null || string.IsNullOrWhiteSpace(setting.ValueJson))
        {
            return (false, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(setting.ValueJson);
            var root = doc.RootElement;
            var confirmed = root.TryGetProperty("confirmed", out var c) && c.GetBoolean();
            DateTimeOffset? at = root.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.String
                ? a.GetDateTimeOffset()
                : null;
            var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
            return (confirmed, at, reason);
        }
        catch (JsonException)
        {
            return (false, null, null);
        }
    }

    private async Task ClearConfirmationAsync(CancellationToken cancellationToken)
    {
        var setting = await db.TenantSettings
            .FirstOrDefaultAsync(x => x.Key == SettingKeys.PayrollRulesConfirmed, cancellationToken)
            .ConfigureAwait(false);
        if (setting is null)
        {
            return;
        }

        setting.SetValue(JsonSerializer.Serialize(new { confirmed = false }));
    }

    private static string? ValidateValueJson(string ruleKey, string valueJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            return ruleKey switch
            {
                PayrollRuleKeys.InsuranceWageMin or PayrollRuleKeys.InsuranceWageMax
                    or PayrollRuleKeys.TaxPersonalExemptionAnnual
                    => RequireDecimal(root, "amount", min: 0),
                PayrollRuleKeys.InsuranceEmployeeRate or PayrollRuleKeys.InsuranceEmployerRate
                    or PayrollRuleKeys.FundMartyrsRate or PayrollRuleKeys.FundEmergencyEmployerRate
                    => RequireDecimal(root, "rate", min: 0, max: 100),
                PayrollRuleKeys.OvertimeMultiplierDay or PayrollRuleKeys.OvertimeMultiplierNight
                    or PayrollRuleKeys.OvertimeMultiplierHoliday
                    => RequireOptionalDecimal(root, "multiplier", min: 0),
                PayrollRuleKeys.PayrollPeriodStartDay
                    => RequireInt(root, "value", min: 1, max: 28),
                PayrollRuleKeys.PayrollSecondApprovalRequired
                    => root.TryGetProperty("value", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? null
                        : "bool value required",
                PayrollRuleKeys.TaxBrackets
                    => root.TryGetProperty("brackets", out var brackets) && brackets.ValueKind == JsonValueKind.Array
                        ? ValidateBrackets(brackets)
                        : "brackets array required",
                PayrollRuleKeys.PayrollWorkingDaysBasis or PayrollRuleKeys.PayrollRounding
                    or PayrollRuleKeys.PayrollTaxMethod
                    => root.TryGetProperty("value", out var e) && e.ValueKind == JsonValueKind.String
                        ? null
                        : "enum value required",
                _ => null,
            };
        }
        catch (JsonException)
        {
            return "invalid json";
        }
    }

    private static string? ValidateBrackets(JsonElement brackets)
    {
        decimal? prevTo = null;
        foreach (var band in brackets.EnumerateArray())
        {
            if (!band.TryGetProperty("from", out var fromEl) || !band.TryGetProperty("to", out var toEl)
                || !band.TryGetProperty("rate", out var rateEl))
            {
                return "bracket requires from, to, rate";
            }

            var from = fromEl.GetDecimal();
            var to = toEl.ValueKind == JsonValueKind.Null ? decimal.MaxValue : toEl.GetDecimal();
            var rate = rateEl.GetDecimal();
            if (from < 0 || to < from || rate < 0 || rate > 100)
            {
                return "invalid bracket range or rate";
            }

            if (prevTo is not null && from != prevTo)
            {
                return "brackets must be contiguous";
            }

            prevTo = to == decimal.MaxValue ? null : to;
        }

        return null;
    }

    private static string? RequireDecimal(JsonElement root, string name, decimal min, decimal? max = null)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return $"{name} required";
        }

        var value = el.GetDecimal();
        if (value < min || (max is not null && value > max))
        {
            return $"{name} out of range";
        }

        return null;
    }

    private static string? RequireOptionalDecimal(JsonElement root, string name, decimal min)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return $"{name} required";
        }

        if (el.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (el.ValueKind != JsonValueKind.Number)
        {
            return $"{name} invalid";
        }

        return el.GetDecimal() < min ? $"{name} out of range" : null;
    }

    private static string? RequireInt(JsonElement root, string name, int min, int max)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
        {
            return $"{name} required";
        }

        var value = el.GetInt32();
        return value < min || value > max ? $"{name} out of range" : null;
    }

    private static string InferValueType(string ruleKey) => ruleKey switch
    {
        PayrollRuleKeys.TaxBrackets => PayrollRuleValueTypes.Table,
        PayrollRuleKeys.PayrollSecondApprovalRequired => PayrollRuleValueTypes.Bool,
        PayrollRuleKeys.PayrollPeriodStartDay => PayrollRuleValueTypes.Int,
        _ when ruleKey.StartsWith("insurance.", StringComparison.Ordinal)
            || ruleKey.StartsWith("fund.", StringComparison.Ordinal)
            || ruleKey == PayrollRuleKeys.TaxPersonalExemptionAnnual
            => ruleKey.Contains("rate", StringComparison.Ordinal)
                ? PayrollRuleValueTypes.Percent
                : PayrollRuleValueTypes.Money,
        _ when ruleKey.StartsWith("overtime.", StringComparison.Ordinal) => PayrollRuleValueTypes.Decimal,
        _ => PayrollRuleValueTypes.Enum,
    };
}
