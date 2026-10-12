using System.Globalization;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Templates;
using Kadr.Platform.Domain.Payroll;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Templates;

public sealed class TemplateApplyService(KadrDbContext db) : ITemplateApplyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task ApplyAsync(Guid tenantId, string industryCode, CancellationToken cancellationToken = default)
    {
        var template = await GetLatestTemplateAsync(industryCode, cancellationToken).ConfigureAwait(false);
        var seed = Deserialize(template);
        AddAllMissing(tenantId, template, seed, existingKeys: null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AddMissingAsync(
        Guid tenantId,
        string industryCode,
        CancellationToken cancellationToken = default)
    {
        var template = await GetLatestTemplateAsync(industryCode, cancellationToken).ConfigureAwait(false);
        var seed = Deserialize(template);
        var existing = await CollectExistingKeysAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var before = CountTrackedAdds();
        AddAllMissing(tenantId, template, seed, existing);
        var added = CountTrackedAdds() - before;
        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return added;
    }

    public async Task<TemplateReplaceResult> ReplaceUntouchedAsync(
        Guid tenantId,
        string industryCode,
        CancellationToken cancellationToken = default)
    {
        var template = await GetLatestTemplateAsync(industryCode, cancellationToken).ConfigureAwait(false);
        var seed = Deserialize(template);
        var existing = await CollectExistingKeysAsync(tenantId, cancellationToken).ConfigureAwait(false);

        // Untouched = copied from a template and never customized. Items that are also part of the new
        // template are kept as-is; everything else is deactivated, never deleted (other data may reference it).
        var deactivated = 0;
        deactivated += await DeactivateUntouchedAsync(
            db.Shifts.Where(x => x.TenantId == tenantId && x.SourceKey != null && !x.IsCustomized && x.IsActive),
            seed.Items.Shifts.Select(x => x.Key),
            x => x.SetActive(false),
            cancellationToken).ConfigureAwait(false);
        deactivated += await DeactivateUntouchedAsync(
            db.LeaveTypes.Where(x => x.TenantId == tenantId && x.SourceKey != null && !x.IsCustomized && x.IsActive),
            seed.Items.LeaveTypes.Select(x => x.Key),
            x => x.SetActive(false),
            cancellationToken).ConfigureAwait(false);
        deactivated += await DeactivateUntouchedAsync(
            db.DocumentTypes.Where(x => x.TenantId == tenantId && x.SourceKey != null && !x.IsCustomized && x.IsActive),
            seed.Items.DocumentTypes.Select(x => x.Key),
            x => x.SetActive(false),
            cancellationToken).ConfigureAwait(false);
        deactivated += await DeactivateUntouchedAsync(
            db.JobTitles.Where(x => x.TenantId == tenantId && x.SourceKey != null && !x.IsCustomized && x.IsActive),
            seed.Items.JobTitles.Select(x => x.Key),
            x => x.SetActive(false),
            cancellationToken).ConfigureAwait(false);
        deactivated += await DeactivateUntouchedAsync(
            db.Departments.Where(x => x.TenantId == tenantId && x.SourceKey != null && !x.IsCustomized && x.IsActive),
            seed.Items.Departments.Select(x => x.Key),
            x => x.SetActive(false),
            cancellationToken).ConfigureAwait(false);

        var before = CountTrackedAdds();
        AddAllMissing(tenantId, template, seed, existing);
        var added = CountTrackedAdds() - before;

        if (added > 0 || deactivated > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new TemplateReplaceResult(added, deactivated);
    }

    private static async Task<int> DeactivateUntouchedAsync<T>(
        IQueryable<T> candidates,
        IEnumerable<string> newTemplateKeys,
        Action<T> deactivate,
        CancellationToken cancellationToken)
        where T : class, ITemplateSourced
    {
        var keep = newTemplateKeys.ToHashSet(StringComparer.Ordinal);
        var items = await candidates.ToListAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;
        foreach (var item in items)
        {
            if (item.SourceKey is { } key && keep.Contains(key))
            {
                continue;
            }

            deactivate(item);
            count++;
        }

        return count;
    }

    private int CountTrackedAdds() =>
        db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);

    private async Task<IndustryTemplate> GetLatestTemplateAsync(
        string industryCode,
        CancellationToken cancellationToken)
    {
        var code = industryCode.Trim().ToLowerInvariant();
        var template = await db.IndustryTemplates.AsNoTracking()
            .Where(t => t.Code == code)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return template
            ?? throw new InvalidOperationException($"Industry template '{code}' is not seeded.");
    }

    private static IndustryTemplateSeedDocument Deserialize(IndustryTemplate template)
    {
        var seed = JsonSerializer.Deserialize<IndustryTemplateSeedDocument>(template.PayloadJson, JsonOptions);
        return seed ?? throw new InvalidOperationException($"Invalid payload for template {template.Code} v{template.Version}.");
    }

    private async Task<HashSet<string>> CollectExistingKeysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        keys.UnionWith(await db.Shifts.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceKey != null)
            .Select(x => x.SourceKey!)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        keys.UnionWith(await db.LeaveTypes.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceKey != null)
            .Select(x => x.SourceKey!)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        keys.UnionWith(await db.DocumentTypes.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceKey != null)
            .Select(x => x.SourceKey!)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        keys.UnionWith(await db.JobTitles.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceKey != null)
            .Select(x => x.SourceKey!)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        keys.UnionWith(await db.Departments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SourceKey != null)
            .Select(x => x.SourceKey!)
            .ToListAsync(cancellationToken).ConfigureAwait(false));
        return keys;
    }

    private void AddAllMissing(
        Guid tenantId,
        IndustryTemplate template,
        IndustryTemplateSeedDocument seed,
        HashSet<string>? existingKeys)
    {
        foreach (var item in seed.Items.Shifts)
        {
            if (existingKeys?.Contains(item.Key) == true)
            {
                continue;
            }

            db.Shifts.Add(Shift.CreateFromTemplate(
                tenantId,
                item.Key,
                template.Id,
                template.Version,
                item.NameAr,
                item.NameEn,
                ParseTime(item.Start),
                ParseTime(item.End),
                item.GraceMinutes));
        }

        foreach (var item in seed.Items.LeaveTypes)
        {
            if (existingKeys?.Contains(item.Key) == true)
            {
                continue;
            }

            string? extras = item.Verify
                ? """{"verify":true}"""
                : null;

            db.LeaveTypes.Add(LeaveType.CreateFromTemplate(
                tenantId,
                item.Key,
                template.Id,
                template.Version,
                item.NameAr,
                item.NameEn,
                item.Paid,
                item.DaysPerYear,
                item.RequiresApproval,
                item.RequiresAttachment,
                extras));
        }

        foreach (var item in seed.Items.DocumentTypes)
        {
            if (existingKeys?.Contains(item.Key) == true)
            {
                continue;
            }

            db.DocumentTypes.Add(DocumentType.CreateFromTemplate(
                tenantId,
                item.Key,
                template.Id,
                template.Version,
                item.NameAr,
                item.NameEn,
                item.HasExpiry,
                JsonSerializer.Serialize(item.AlertDays)));
        }

        foreach (var item in seed.Items.JobTitles)
        {
            if (existingKeys?.Contains(item.Key) == true)
            {
                continue;
            }

            db.JobTitles.Add(JobTitle.CreateFromTemplate(
                tenantId,
                item.Key,
                template.Id,
                template.Version,
                item.NameAr,
                item.NameEn));
        }

        foreach (var item in seed.Items.Departments)
        {
            if (existingKeys?.Contains(item.Key) == true)
            {
                continue;
            }

            db.Departments.Add(Department.CreateFromTemplate(
                tenantId,
                item.Key,
                template.Id,
                template.Version,
                item.NameAr,
                item.NameEn));
        }

        // First industry apply (existingKeys == null): seed the default basic pay component.
        // Existing tenants without components get it on first pay-components list (EnsureBasicSeeded).
        if (existingKeys is null
            && !db.PayComponents.Local.Any(c => c.TenantId == tenantId && c.Key == PayComponent.BasicKey))
        {
            db.PayComponents.Add(PayComponent.CreateBasic(tenantId));
        }
    }

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.ParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture);
}
