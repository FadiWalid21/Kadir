using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Text;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class PayComponentService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit) : IPayComponentService
{
    public async Task<Result<PagedPayComponentsDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        await EnsureBasicSeededAsync(cancellationToken).ConfigureAwait(false);

        var term = ArabicText.NormalizeForSearch(q);
        var query = db.PayComponents.AsNoTracking();

        // Default list shows active only when filter omitted.
        if (isActive is null)
        {
            query = query.Where(c => c.IsActive);
        }
        else
        {
            query = query.Where(c => c.IsActive == isActive);
        }

        if (term.Length > 0)
        {
            query = query.Where(c => c.SearchName.Contains(term) || c.Key.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderBy(c => c.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Key,
                c.NameAr,
                c.NameEn,
                c.Kind,
                c.CalcType,
                c.IsTaxable,
                c.IsInsurable,
                c.IsActive,
                c.SourceKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(c => new PayComponentDto(
                c.Id,
                c.Key,
                c.NameAr,
                c.NameEn,
                c.Kind.ToString(),
                c.CalcType.ToString(),
                c.IsTaxable,
                c.IsInsurable,
                c.IsActive,
                c.SourceKey))
            .ToList();

        return Result.Success(new PagedPayComponentsDto(items, page, pageSize, total));
    }

    public async Task<Result<PayComponentDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await db.PayComponents.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Key,
                c.NameAr,
                c.NameEn,
                c.Kind,
                c.CalcType,
                c.IsTaxable,
                c.IsInsurable,
                c.IsActive,
                c.SourceKey,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null
            ? Result.Failure<PayComponentDto>(ErrorCodes.SalaryNotFound, ErrorCodes.SalaryNotFound)
            : Result.Success(new PayComponentDto(
                row.Id,
                row.Key,
                row.NameAr,
                row.NameEn,
                row.Kind.ToString(),
                row.CalcType.ToString(),
                row.IsTaxable,
                row.IsInsurable,
                row.IsActive,
                row.SourceKey));
    }

    public async Task<Result<PayComponentDto>> CreateAsync(
        CreatePayComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEnums(request.Kind, request.CalcType, out var kind, out var calcType))
        {
            return Result.Failure<PayComponentDto>(
                ErrorCodes.SalaryValidationFailed,
                ErrorCodes.SalaryValidationFailed);
        }

        var key = request.Key.Trim().ToLowerInvariant();
        var exists = await db.PayComponents.AsNoTracking()
            .AnyAsync(c => c.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return Result.Failure<PayComponentDto>(
                ErrorCodes.SalaryDuplicateKey,
                ErrorCodes.SalaryDuplicateKey);
        }

        var tenantId = RequireTenantId();
        var entity = PayComponent.Create(
            tenantId,
            key,
            request.NameAr,
            request.NameEn,
            kind,
            calcType,
            request.IsTaxable,
            request.IsInsurable);
        db.PayComponents.Add(entity);
        var dto = MapDto(entity);
        StageAudit("pay_component.create", entity.Id, null, JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    public async Task<Result<PayComponentDto>> UpdateAsync(
        Guid id,
        UpdatePayComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEnums(request.Kind, request.CalcType, out var kind, out var calcType))
        {
            return Result.Failure<PayComponentDto>(
                ErrorCodes.SalaryValidationFailed,
                ErrorCodes.SalaryValidationFailed);
        }

        var entity = await db.PayComponents.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<PayComponentDto>(ErrorCodes.SalaryNotFound, ErrorCodes.SalaryNotFound);
        }

        var oldDto = MapDto(entity);
        try
        {
            entity.Update(
                request.NameAr,
                request.NameEn,
                kind,
                calcType,
                request.IsTaxable,
                request.IsInsurable);
        }
        catch (ArgumentException)
        {
            return Result.Failure<PayComponentDto>(
                ErrorCodes.SalaryValidationFailed,
                ErrorCodes.SalaryValidationFailed);
        }

        var dto = MapDto(entity);
        StageAudit(
            "pay_component.update",
            entity.Id,
            JsonSerializer.Serialize(oldDto),
            JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    public async Task<Result<PayComponentDto>> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PayComponents.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<PayComponentDto>(ErrorCodes.SalaryNotFound, ErrorCodes.SalaryNotFound);
        }

        if (!entity.IsActive)
        {
            return Result.Success(MapDto(entity));
        }

        var oldDto = MapDto(entity);
        entity.Deactivate();
        var dto = MapDto(entity);
        StageAudit(
            "pay_component.deactivate",
            entity.Id,
            JsonSerializer.Serialize(oldDto),
            JsonSerializer.Serialize(dto));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dto);
    }

    private async Task EnsureBasicSeededAsync(CancellationToken cancellationToken)
    {
        var any = await db.PayComponents.AsNoTracking()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (any)
        {
            return;
        }

        var tenantId = RequireTenantId();
        db.PayComponents.Add(PayComponent.CreateBasic(tenantId));
        StageAudit("pay_component.seed_basic", null, null, null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool TryParseEnums(
        string kindRaw,
        string calcTypeRaw,
        out PayComponentKind kind,
        out PayComponentCalcType calcType)
    {
        var kindOk = Enum.TryParse(kindRaw, ignoreCase: true, out kind);
        var calcOk = Enum.TryParse(calcTypeRaw, ignoreCase: true, out calcType);
        return kindOk && calcOk;
    }

    private static PayComponentDto MapDto(PayComponent c) =>
        new(
            c.Id,
            c.Key,
            c.NameAr,
            c.NameEn,
            c.Kind.ToString(),
            c.CalcType.ToString(),
            c.IsTaxable,
            c.IsInsurable,
            c.IsActive,
            c.SourceKey);

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
            "pay_component",
            entityId,
            oldJson,
            newJson);
    }
}
