using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Branches;
using Kadr.Platform.Domain.Org;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Text;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Branches;

public sealed class BranchSettingsService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit) : IBranchSettingsService
{
    public async Task<Result<PagedBranchesDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        bool? active,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = ArabicText.NormalizeForSearch(q);

        var query = db.Branches.AsNoTracking();
        if (active is not null)
        {
            query = query.Where(b => b.IsActive == active);
        }

        if (term.Length > 0)
        {
            query = query.Where(b => b.SearchName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await (
                from b in query
                join le in db.LegalEntities.AsNoTracking() on b.LegalEntityId equals le.Id
                orderby b.NameAr
                select new
                {
                    b.Id,
                    b.LegalEntityId,
                    LegalEntityName = le.Name,
                    b.NameAr,
                    b.NameEn,
                    b.Address,
                    b.IsDefault,
                    b.IsActive,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new BranchListItemDto(
                r.Id,
                r.LegalEntityId,
                r.LegalEntityName,
                r.NameAr,
                r.NameEn,
                r.Address,
                r.IsDefault,
                r.IsActive))
            .ToList();

        return Result.Success(new PagedBranchesDto(items, page, pageSize, total));
    }

    public async Task<Result<BranchListItemDto>> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var legalOk = await EnsureLegalEntityAsync(request.LegalEntityId, cancellationToken)
            .ConfigureAwait(false);
        if (!legalOk.IsSuccess)
        {
            return Result.Failure<BranchListItemDto>(legalOk.ErrorCode!, legalOk.ErrorMessage!);
        }

        if (await HasDuplicateNamesAsync(request.NameAr, request.NameEn, null, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<BranchListItemDto>(
                ErrorCodes.BranchesDuplicateName,
                ErrorCodes.BranchesDuplicateName);
        }

        var tenantId = RequireTenantId();
        var entity = Branch.Create(
            tenantId,
            request.LegalEntityId,
            request.NameAr,
            request.NameEn,
            request.Address);
        db.Branches.Add(entity);
        var dto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        StageAudit("branch.create", entity.Id, null, JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    public async Task<Result<BranchListItemDto>> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<BranchListItemDto>(ErrorCodes.BranchesNotFound, ErrorCodes.BranchesNotFound);
        }

        var legalOk = await EnsureLegalEntityAsync(request.LegalEntityId, cancellationToken)
            .ConfigureAwait(false);
        if (!legalOk.IsSuccess)
        {
            return Result.Failure<BranchListItemDto>(legalOk.ErrorCode!, legalOk.ErrorMessage!);
        }

        if (await HasDuplicateNamesAsync(request.NameAr, request.NameEn, id, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<BranchListItemDto>(
                ErrorCodes.BranchesDuplicateName,
                ErrorCodes.BranchesDuplicateName);
        }

        var oldDto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        entity.Update(request.LegalEntityId, request.NameAr, request.NameEn, request.Address);
        var dto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        StageAudit("branch.update", entity.Id, JsonSerializer.Serialize(oldDto), JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    public async Task<Result<BranchListItemDto>> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<BranchListItemDto>(ErrorCodes.BranchesNotFound, ErrorCodes.BranchesNotFound);
        }

        if (!isActive && entity.IsActive)
        {
            var otherActive = await db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id != id && b.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!otherActive)
            {
                return Result.Failure<BranchListItemDto>(
                    ErrorCodes.BranchesLastActive,
                    ErrorCodes.BranchesLastActive);
            }
        }

        var oldDto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        entity.SetActive(isActive);
        var dto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        StageAudit("branch.set_active", entity.Id, JsonSerializer.Serialize(oldDto), JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure(ErrorCodes.BranchesNotFound, ErrorCodes.BranchesNotFound);
        }

        var inUse =
            await db.Employees.AsNoTracking().AnyAsync(e => e.BranchId == id, cancellationToken)
                .ConfigureAwait(false)
            || await db.AttendancePunches.AsNoTracking().AnyAsync(p => p.BranchId == id, cancellationToken)
                .ConfigureAwait(false)
            || await db.WorkLocations.AsNoTracking().AnyAsync(w => w.LinkedBranchId == id, cancellationToken)
                .ConfigureAwait(false)
            || await db.WorkLocationAssignments.AsNoTracking()
                .AnyAsync(a => a.BranchId == id, cancellationToken)
                .ConfigureAwait(false);
        if (inUse)
        {
            return Result.Failure(ErrorCodes.BranchesInUse, ErrorCodes.BranchesInUse);
        }

        if (entity.IsActive)
        {
            var otherActive = await db.Branches.AsNoTracking()
                .AnyAsync(b => b.Id != id && b.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!otherActive)
            {
                return Result.Failure(ErrorCodes.BranchesLastActive, ErrorCodes.BranchesLastActive);
            }
        }

        var oldDto = await MapAsync(entity, cancellationToken).ConfigureAwait(false);
        db.Branches.Remove(entity);
        StageAudit("branch.delete", id, JsonSerializer.Serialize(oldDto), null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> EnsureLegalEntityAsync(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var exists = await db.LegalEntities.AsNoTracking()
            .AnyAsync(x => x.Id == legalEntityId, cancellationToken)
            .ConfigureAwait(false);
        return exists
            ? Result.Success()
            : Result.Failure(ErrorCodes.BranchesLegalEntityNotFound, ErrorCodes.BranchesLegalEntityNotFound);
    }

    private async Task<bool> HasDuplicateNamesAsync(
        string nameAr,
        string nameEn,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var nameArNorm = ArabicText.NormalizeForSearch(nameAr);
        var nameEnNorm = ArabicText.NormalizeForSearch(nameEn);
        var rows = await db.Branches.AsNoTracking()
            .Select(b => new { b.Id, b.NameAr, b.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Any(x =>
            (excludeId is null || x.Id != excludeId)
            && (ArabicText.NormalizeForSearch(x.NameAr) == nameArNorm
                || ArabicText.NormalizeForSearch(x.NameEn) == nameEnNorm));
    }

    private async Task<BranchListItemDto> MapAsync(Branch entity, CancellationToken cancellationToken)
    {
        var legalName = await db.LegalEntities.AsNoTracking()
            .Where(x => x.Id == entity.LegalEntityId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;
        return new BranchListItemDto(
            entity.Id,
            entity.LegalEntityId,
            legalName,
            entity.NameAr,
            entity.NameEn,
            entity.Address,
            entity.IsDefault,
            entity.IsActive);
    }

    private Guid RequireTenantId()
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return tenantContext.TenantId.Value;
    }

    private void StageAudit(string action, Guid? entityId, string? oldJson, string? newJson)
    {
        if (tenantContext.TenantId is null)
        {
            return;
        }

        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            action,
            "branch",
            entityId,
            oldJson,
            newJson);
    }
}
