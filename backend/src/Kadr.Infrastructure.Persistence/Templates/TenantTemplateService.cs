using System.Globalization;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Templates;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Templates;

public sealed class TenantTemplateService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ITemplateApplyService applyService,
    IAuditLogWriter audit) : ITenantTemplateService
{
    public const string SectionShifts = "shifts";
    public const string SectionLeaveTypes = "leave-types";
    public const string SectionDocumentTypes = "document-types";
    public const string SectionJobTitles = "job-titles";
    public const string SectionDepartments = "departments";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<Result<TenantTemplatesOverviewDto>> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<TenantTemplatesOverviewDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var catalog = IndustryCodes.Catalog.FirstOrDefault(c =>
            string.Equals(c.Code, tenant.IndustryCode, StringComparison.OrdinalIgnoreCase));
        var latest = await db.IndustryTemplates.AsNoTracking()
            .Where(t => t.Code == tenant.IndustryCode)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (latest is null)
        {
            return Result.Failure<TenantTemplatesOverviewDto>(
                ErrorCodes.TemplateSourceMissing,
                ErrorCodes.TemplateSourceMissing);
        }

        var seed = Deserialize(latest);
        var existingKeys = await CollectKeysAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
        var allSeedKeys = AllSeedKeys(seed);
        var missing = allSeedKeys.Where(k => !existingKeys.Contains(k)).OrderBy(k => k).ToList();
        var appliedVersion = await MaxAppliedVersionAsync(tenant.Id, cancellationToken).ConfigureAwait(false);

        return Result.Success(new TenantTemplatesOverviewDto(
            tenant.IndustryCode,
            catalog?.NameAr ?? tenant.IndustryCode,
            catalog?.NameEn ?? tenant.IndustryCode,
            appliedVersion,
            latest.Version,
            latest.Version > appliedVersion || missing.Count > 0,
            missing));
    }

    public async Task<Result<PagedResultDto<TemplateItemDto>>> ListSectionAsync(
        string section,
        int page,
        int pageSize,
        string? q,
        bool? active,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<PagedResultDto<TemplateItemDto>>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        if (!IsValidSection(section))
        {
            return Result.Failure<PagedResultDto<TemplateItemDto>>(
                ErrorCodes.TemplateSectionInvalid,
                ErrorCodes.TemplateSectionInvalid);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var qNorm = string.IsNullOrWhiteSpace(q) ? null : q.Trim().ToLowerInvariant();

        return section switch
        {
            SectionShifts => await PageAsync(
                db.Shifts.AsNoTracking(),
                qNorm,
                active,
                page,
                pageSize,
                x => MapShift(x),
                cancellationToken).ConfigureAwait(false),
            SectionLeaveTypes => await PageAsync(
                db.LeaveTypes.AsNoTracking(),
                qNorm,
                active,
                page,
                pageSize,
                x => MapLeave(x),
                cancellationToken).ConfigureAwait(false),
            SectionDocumentTypes => await PageAsync(
                db.DocumentTypes.AsNoTracking(),
                qNorm,
                active,
                page,
                pageSize,
                x => MapDoc(x),
                cancellationToken).ConfigureAwait(false),
            SectionJobTitles => await PageAsync(
                db.JobTitles.AsNoTracking(),
                qNorm,
                active,
                page,
                pageSize,
                x => MapJob(x),
                cancellationToken).ConfigureAwait(false),
            _ => await PageAsync(
                db.Departments.AsNoTracking(),
                qNorm,
                active,
                page,
                pageSize,
                x => MapDept(x),
                cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task<Result<TemplateItemDto>> UpdateShiftAsync(
        Guid id,
        UpdateShiftTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseTime(request.StartTime, out var start) || !TryParseTime(request.EndTime, out var end)
            || request.GraceMinutes < 0
            || string.IsNullOrWhiteSpace(request.NameAr)
            || string.IsNullOrWhiteSpace(request.NameEn))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateValidationFailed,
                ErrorCodes.TemplateValidationFailed);
        }

        var entity = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        var oldJson = JsonSerializer.Serialize(MapShift(entity));
        entity.Update(request.NameAr, request.NameEn, start, end, request.GraceMinutes);
        StageAudit("template.update", SectionShifts, entity.Id, oldJson, JsonSerializer.Serialize(MapShift(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapShift(entity));
    }

    public async Task<Result<TemplateItemDto>> UpdateLeaveTypeAsync(
        Guid id,
        UpdateLeaveTypeTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn)
            || request.DaysPerYear is < 0)
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateValidationFailed,
                ErrorCodes.TemplateValidationFailed);
        }

        var entity = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        var oldJson = JsonSerializer.Serialize(MapLeave(entity));
        entity.Update(
            request.NameAr,
            request.NameEn,
            request.Paid,
            request.DaysPerYear,
            request.RequiresApproval,
            request.RequiresAttachment,
            entity.ExtrasJson);
        StageAudit("template.update", SectionLeaveTypes, entity.Id, oldJson, JsonSerializer.Serialize(MapLeave(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapLeave(entity));
    }

    public async Task<Result<TemplateItemDto>> UpdateDocumentTypeAsync(
        Guid id,
        UpdateDocumentTypeTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateValidationFailed,
                ErrorCodes.TemplateValidationFailed);
        }

        var entity = await db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        var oldJson = JsonSerializer.Serialize(MapDoc(entity));
        entity.Update(
            request.NameAr,
            request.NameEn,
            request.HasExpiry,
            JsonSerializer.Serialize(request.AlertDays ?? []));
        StageAudit("template.update", SectionDocumentTypes, entity.Id, oldJson, JsonSerializer.Serialize(MapDoc(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapDoc(entity));
    }

    public async Task<Result<TemplateItemDto>> UpdateJobTitleAsync(
        Guid id,
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateValidationFailed,
                ErrorCodes.TemplateValidationFailed);
        }

        var entity = await db.JobTitles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        var oldJson = JsonSerializer.Serialize(MapJob(entity));
        entity.Update(request.NameAr, request.NameEn);
        StageAudit("template.update", SectionJobTitles, entity.Id, oldJson, JsonSerializer.Serialize(MapJob(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapJob(entity));
    }

    public async Task<Result<TemplateItemDto>> UpdateDepartmentAsync(
        Guid id,
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateValidationFailed,
                ErrorCodes.TemplateValidationFailed);
        }

        var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        var oldJson = JsonSerializer.Serialize(MapDept(entity));
        entity.Update(request.NameAr, request.NameEn);
        StageAudit("template.update", SectionDepartments, entity.Id, oldJson, JsonSerializer.Serialize(MapDept(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapDept(entity));
    }

    public async Task<Result<TemplateItemDto>> ResetAsync(
        string section,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidSection(section))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateSectionInvalid,
                ErrorCodes.TemplateSectionInvalid);
        }

        return section switch
        {
            SectionShifts => await ResetShiftAsync(id, cancellationToken).ConfigureAwait(false),
            SectionLeaveTypes => await ResetLeaveAsync(id, cancellationToken).ConfigureAwait(false),
            SectionDocumentTypes => await ResetDocAsync(id, cancellationToken).ConfigureAwait(false),
            SectionJobTitles => await ResetJobAsync(id, cancellationToken).ConfigureAwait(false),
            _ => await ResetDeptAsync(id, cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task<Result<TemplateItemDto>> SetActiveAsync(
        string section,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidSection(section))
        {
            return Result.Failure<TemplateItemDto>(
                ErrorCodes.TemplateSectionInvalid,
                ErrorCodes.TemplateSectionInvalid);
        }

        TemplateItemDto? dto = null;
        Guid? entityId = null;
        string? oldJson = null;

        switch (section)
        {
            case SectionShifts:
            {
                var entity = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
                }

                oldJson = JsonSerializer.Serialize(MapShift(entity));
                entity.SetActive(isActive);
                entityId = entity.Id;
                dto = MapShift(entity);
                break;
            }
            case SectionLeaveTypes:
            {
                var entity = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
                }

                oldJson = JsonSerializer.Serialize(MapLeave(entity));
                entity.SetActive(isActive);
                entityId = entity.Id;
                dto = MapLeave(entity);
                break;
            }
            case SectionDocumentTypes:
            {
                var entity = await db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
                }

                oldJson = JsonSerializer.Serialize(MapDoc(entity));
                entity.SetActive(isActive);
                entityId = entity.Id;
                dto = MapDoc(entity);
                break;
            }
            case SectionJobTitles:
            {
                var entity = await db.JobTitles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
                }

                oldJson = JsonSerializer.Serialize(MapJob(entity));
                entity.SetActive(isActive);
                entityId = entity.Id;
                dto = MapJob(entity);
                break;
            }
            default:
            {
                var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
                }

                oldJson = JsonSerializer.Serialize(MapDept(entity));
                entity.SetActive(isActive);
                entityId = entity.Id;
                dto = MapDept(entity);
                break;
            }
        }

        StageAudit(
                "template.set_active",
                section,
                entityId,
                oldJson,
                JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto!);
    }

    public async Task<Result<AddMissingResultDto>> AddMissingAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantAsync(cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<AddMissingResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var beforeKeys = await CollectKeysAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
        var added = await applyService.AddMissingAsync(tenant.Id, tenant.IndustryCode, cancellationToken)
            .ConfigureAwait(false);
        var afterKeys = await CollectKeysAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
        var newKeys = afterKeys.Where(k => !beforeKeys.Contains(k)).OrderBy(k => k).ToList();
        if (tenantContext.TenantId is not null)
        {
            await audit.WriteAsync(
                    tenantContext.TenantId.Value,
                    tenantContext.UserId,
                    "template.add_missing",
                    "templates",
                    null,
                    null,
                    JsonSerializer.Serialize(new { added, keys = newKeys }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success(new AddMissingResultDto(added, newKeys));
    }

    private async Task<Result<TemplateItemDto>> ResetShiftAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.SourceKey))
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var seedItem = await FindSeedShiftAsync(entity.SourceKey, entity.SourceVersion, cancellationToken)
            .ConfigureAwait(false);
        if (seedItem is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var oldJson = JsonSerializer.Serialize(MapShift(entity));
        entity.ResetFromTemplate(
            seedItem.NameAr,
            seedItem.NameEn,
            ParseTime(seedItem.Start),
            ParseTime(seedItem.End),
            seedItem.GraceMinutes,
            entity.SourceVersion);
        StageAudit("template.reset", SectionShifts, entity.Id, oldJson, JsonSerializer.Serialize(MapShift(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapShift(entity));
    }

    private async Task<Result<TemplateItemDto>> ResetLeaveAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.LeaveTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.SourceKey))
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var seedItem = await FindSeedLeaveAsync(entity.SourceKey, entity.SourceVersion, cancellationToken)
            .ConfigureAwait(false);
        if (seedItem is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var oldJson = JsonSerializer.Serialize(MapLeave(entity));
        var extras = seedItem.Verify ? """{"verify":true}""" : null;
        entity.ResetFromTemplate(
            seedItem.NameAr,
            seedItem.NameEn,
            seedItem.Paid,
            seedItem.DaysPerYear,
            seedItem.RequiresApproval,
            seedItem.RequiresAttachment,
            extras,
            entity.SourceVersion);
        StageAudit("template.reset", SectionLeaveTypes, entity.Id, oldJson, JsonSerializer.Serialize(MapLeave(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapLeave(entity));
    }

    private async Task<Result<TemplateItemDto>> ResetDocAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.SourceKey))
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var seedItem = await FindSeedDocAsync(entity.SourceKey, entity.SourceVersion, cancellationToken)
            .ConfigureAwait(false);
        if (seedItem is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var oldJson = JsonSerializer.Serialize(MapDoc(entity));
        entity.ResetFromTemplate(
            seedItem.NameAr,
            seedItem.NameEn,
            seedItem.HasExpiry,
            JsonSerializer.Serialize(seedItem.AlertDays),
            entity.SourceVersion);
        StageAudit("template.reset", SectionDocumentTypes, entity.Id, oldJson, JsonSerializer.Serialize(MapDoc(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapDoc(entity));
    }

    private async Task<Result<TemplateItemDto>> ResetJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.JobTitles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.SourceKey))
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var seedItem = await FindSeedNamedAsync(entity.SourceKey, entity.SourceVersion, "jobTitles", cancellationToken)
            .ConfigureAwait(false);
        if (seedItem is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var oldJson = JsonSerializer.Serialize(MapJob(entity));
        entity.ResetFromTemplate(seedItem.NameAr, seedItem.NameEn, entity.SourceVersion);
        StageAudit("template.reset", SectionJobTitles, entity.Id, oldJson, JsonSerializer.Serialize(MapJob(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapJob(entity));
    }

    private async Task<Result<TemplateItemDto>> ResetDeptAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateItemNotFound, ErrorCodes.TemplateItemNotFound);
        }

        if (string.IsNullOrWhiteSpace(entity.SourceKey))
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var seedItem = await FindSeedNamedAsync(entity.SourceKey, entity.SourceVersion, "departments", cancellationToken)
            .ConfigureAwait(false);
        if (seedItem is null)
        {
            return Result.Failure<TemplateItemDto>(ErrorCodes.TemplateSourceMissing, ErrorCodes.TemplateSourceMissing);
        }

        var oldJson = JsonSerializer.Serialize(MapDept(entity));
        entity.ResetFromTemplate(seedItem.NameAr, seedItem.NameEn, entity.SourceVersion);
        StageAudit("template.reset", SectionDepartments, entity.Id, oldJson, JsonSerializer.Serialize(MapDept(entity)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapDept(entity));
    }

    private void StageAudit(
        string action,
        string entityType,
        Guid? entityId,
        string? oldJson,
        string? newJson)
    {
        if (tenantContext.TenantId is null)
        {
            return;
        }

        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            action,
            entityType,
            entityId,
            oldJson,
            newJson);
    }

    private async Task<Platform.Domain.Tenants.Tenant?> GetTenantAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return null;
        }

        return await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<HashSet<string>> CollectKeysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        keys.UnionWith(await db.Shifts.AsNoTracking().Where(x => x.TenantId == tenantId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken));
        keys.UnionWith(await db.LeaveTypes.AsNoTracking().Where(x => x.TenantId == tenantId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken));
        keys.UnionWith(await db.DocumentTypes.AsNoTracking().Where(x => x.TenantId == tenantId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken));
        keys.UnionWith(await db.JobTitles.AsNoTracking().Where(x => x.TenantId == tenantId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken));
        keys.UnionWith(await db.Departments.AsNoTracking().Where(x => x.TenantId == tenantId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken));
        return keys;
    }

    private async Task<int> MaxAppliedVersionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var versions = new List<int>();
        versions.AddRange(await db.Shifts.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.SourceVersion).ToListAsync(cancellationToken));
        versions.AddRange(await db.LeaveTypes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.SourceVersion).ToListAsync(cancellationToken));
        versions.AddRange(await db.DocumentTypes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.SourceVersion).ToListAsync(cancellationToken));
        versions.AddRange(await db.JobTitles.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.SourceVersion).ToListAsync(cancellationToken));
        versions.AddRange(await db.Departments.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => x.SourceVersion).ToListAsync(cancellationToken));
        return versions.Count == 0 ? 0 : versions.Max();
    }

    private static IReadOnlyList<string> AllSeedKeys(IndustryTemplateSeedDocument seed) =>
        seed.Items.Shifts.Select(x => x.Key)
            .Concat(seed.Items.LeaveTypes.Select(x => x.Key))
            .Concat(seed.Items.DocumentTypes.Select(x => x.Key))
            .Concat(seed.Items.JobTitles.Select(x => x.Key))
            .Concat(seed.Items.Departments.Select(x => x.Key))
            .ToList();

    private async Task<IndustryTemplateSeedDocument?> LoadSeedAsync(int version, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            return null;
        }

        var row = await db.IndustryTemplates.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Code == tenant.IndustryCode && t.Version == version, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Deserialize(row);
    }

    private async Task<ShiftSeedItem?> FindSeedShiftAsync(string key, int version, CancellationToken cancellationToken)
    {
        var seed = await LoadSeedAsync(version, cancellationToken).ConfigureAwait(false);
        return seed?.Items.Shifts.FirstOrDefault(x => x.Key == key);
    }

    private async Task<LeaveTypeSeedItem?> FindSeedLeaveAsync(string key, int version, CancellationToken cancellationToken)
    {
        var seed = await LoadSeedAsync(version, cancellationToken).ConfigureAwait(false);
        return seed?.Items.LeaveTypes.FirstOrDefault(x => x.Key == key);
    }

    private async Task<DocumentTypeSeedItem?> FindSeedDocAsync(string key, int version, CancellationToken cancellationToken)
    {
        var seed = await LoadSeedAsync(version, cancellationToken).ConfigureAwait(false);
        return seed?.Items.DocumentTypes.FirstOrDefault(x => x.Key == key);
    }

    private async Task<NamedSeedItem?> FindSeedNamedAsync(
        string key,
        int version,
        string kind,
        CancellationToken cancellationToken)
    {
        var seed = await LoadSeedAsync(version, cancellationToken).ConfigureAwait(false);
        if (seed is null)
        {
            return null;
        }

        return kind == "jobTitles"
            ? seed.Items.JobTitles.FirstOrDefault(x => x.Key == key)
            : seed.Items.Departments.FirstOrDefault(x => x.Key == key);
    }

    private static IndustryTemplateSeedDocument Deserialize(IndustryTemplate template) =>
        JsonSerializer.Deserialize<IndustryTemplateSeedDocument>(template.PayloadJson, JsonOptions)
        ?? throw new InvalidOperationException("Invalid template payload.");

    private async Task<Result<PagedResultDto<TemplateItemDto>>> PageAsync(
        IQueryable<Shift> query,
        string? qNorm,
        bool? active,
        int page,
        int pageSize,
        Func<Shift, TemplateItemDto> map,
        CancellationToken cancellationToken)
    {
        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        if (qNorm is not null)
        {
            query = query.Where(x => x.SearchName.Contains(qNorm));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(x => x.NameAr).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResultDto<TemplateItemDto>(items.Select(map).ToList(), page, pageSize, total));
    }

    private async Task<Result<PagedResultDto<TemplateItemDto>>> PageAsync(
        IQueryable<LeaveType> query,
        string? qNorm,
        bool? active,
        int page,
        int pageSize,
        Func<LeaveType, TemplateItemDto> map,
        CancellationToken cancellationToken)
    {
        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        if (qNorm is not null)
        {
            query = query.Where(x => x.SearchName.Contains(qNorm));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(x => x.NameAr).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResultDto<TemplateItemDto>(items.Select(map).ToList(), page, pageSize, total));
    }

    private async Task<Result<PagedResultDto<TemplateItemDto>>> PageAsync(
        IQueryable<DocumentType> query,
        string? qNorm,
        bool? active,
        int page,
        int pageSize,
        Func<DocumentType, TemplateItemDto> map,
        CancellationToken cancellationToken)
    {
        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        if (qNorm is not null)
        {
            query = query.Where(x => x.SearchName.Contains(qNorm));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(x => x.NameAr).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResultDto<TemplateItemDto>(items.Select(map).ToList(), page, pageSize, total));
    }

    private async Task<Result<PagedResultDto<TemplateItemDto>>> PageAsync(
        IQueryable<JobTitle> query,
        string? qNorm,
        bool? active,
        int page,
        int pageSize,
        Func<JobTitle, TemplateItemDto> map,
        CancellationToken cancellationToken)
    {
        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        if (qNorm is not null)
        {
            query = query.Where(x => x.SearchName.Contains(qNorm));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(x => x.NameAr).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResultDto<TemplateItemDto>(items.Select(map).ToList(), page, pageSize, total));
    }

    private async Task<Result<PagedResultDto<TemplateItemDto>>> PageAsync(
        IQueryable<Department> query,
        string? qNorm,
        bool? active,
        int page,
        int pageSize,
        Func<Department, TemplateItemDto> map,
        CancellationToken cancellationToken)
    {
        if (active is not null)
        {
            query = query.Where(x => x.IsActive == active);
        }

        if (qNorm is not null)
        {
            query = query.Where(x => x.SearchName.Contains(qNorm));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(x => x.NameAr).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedResultDto<TemplateItemDto>(items.Select(map).ToList(), page, pageSize, total));
    }

    private static bool IsValidSection(string section) =>
        section is SectionShifts or SectionLeaveTypes or SectionDocumentTypes or SectionJobTitles or SectionDepartments;

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.ParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture);

    private static TemplateItemDto MapShift(Shift x) =>
        new(x.Id, SectionShifts, x.SourceKey, x.SourceVersion, x.IsCustomized, x.IsActive, x.NameAr, x.NameEn,
            new Dictionary<string, object?>
            {
                ["startTime"] = x.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                ["endTime"] = x.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                ["graceMinutes"] = x.GraceMinutes,
            });

    private static TemplateItemDto MapLeave(LeaveType x) =>
        new(x.Id, SectionLeaveTypes, x.SourceKey, x.SourceVersion, x.IsCustomized, x.IsActive, x.NameAr, x.NameEn,
            new Dictionary<string, object?>
            {
                ["paid"] = x.Paid,
                ["daysPerYear"] = x.DaysPerYear,
                ["requiresApproval"] = x.RequiresApproval,
                ["requiresAttachment"] = x.RequiresAttachment,
                ["extrasJson"] = x.ExtrasJson,
            });

    private static TemplateItemDto MapDoc(DocumentType x)
    {
        int[] alertDays;
        try
        {
            alertDays = JsonSerializer.Deserialize<int[]>(x.AlertDaysJson) ?? [];
        }
        catch (JsonException)
        {
            alertDays = [];
        }

        return new(x.Id, SectionDocumentTypes, x.SourceKey, x.SourceVersion, x.IsCustomized, x.IsActive, x.NameAr, x.NameEn,
            new Dictionary<string, object?>
            {
                ["hasExpiry"] = x.HasExpiry,
                ["alertDays"] = alertDays,
            });
    }

    private static TemplateItemDto MapJob(JobTitle x) =>
        new(x.Id, SectionJobTitles, x.SourceKey, x.SourceVersion, x.IsCustomized, x.IsActive, x.NameAr, x.NameEn,
            new Dictionary<string, object?>());

    private static TemplateItemDto MapDept(Department x) =>
        new(x.Id, SectionDepartments, x.SourceKey, x.SourceVersion, x.IsCustomized, x.IsActive, x.NameAr, x.NameEn,
            new Dictionary<string, object?>());
}
