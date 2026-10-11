using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence.Auth;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Roles;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Roles;

public sealed class RoleService(
    KadrDbContext db,
    ITenantContext tenantContext,
    PrivilegeGuard privilegeGuard,
    IPermissionCacheInvalidator cacheInvalidator,
    IAuditLogWriter audit) : IRoleService
{
    public async Task<Result<IReadOnlyList<RoleListItemDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<IReadOnlyList<RoleListItemDto>>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        var roles = await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.IsSystem,
                r.DescriptionAr,
                r.DescriptionEn,
                PermissionCount = db.RolePermissions.Count(rp => rp.RoleId == r.Id),
                UserCount = db.UserRoles.Count(ur => ur.RoleId == r.Id),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<RoleListItemDto> dto = roles
            .Select(r => new RoleListItemDto(
                r.Id,
                r.Name,
                r.IsSystem,
                r.DescriptionAr,
                r.DescriptionEn,
                r.PermissionCount,
                r.UserCount))
            .ToList();
        return Result.Success(dto);
    }

    public async Task<Result<RoleDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<RoleDetailDto>(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        return Result.Success(await ToDetailAsync(role, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<RoleDetailDto>> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<RoleDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<RoleDetailDto>(
                ErrorCodes.RolesValidationFailed,
                ErrorCodes.RolesValidationFailed);
        }

        var perms = NormalizePermissions(request.Permissions);
        var grant = await privilegeGuard.EnsureCanGrantAsync(perms, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return Result.Failure<RoleDetailDto>(grant.ErrorCode!, grant.ErrorMessage!);
        }

        var tenantId = tenantContext.TenantId.Value;
        var nameTaken = await db.Roles.AsNoTracking()
            .AnyAsync(r => r.Name == request.Name.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (nameTaken)
        {
            return Result.Failure<RoleDetailDto>(
                ErrorCodes.RolesValidationFailed,
                ErrorCodes.RolesValidationFailed);
        }

        var role = Role.CreateCustom(tenantId, request.Name, request.DescriptionAr, request.DescriptionEn);
        db.Roles.Add(role);
        foreach (var code in perms)
        {
            db.RolePermissions.Add(RolePermission.Create(tenantId, role.Id, code));
        }

        audit.Stage(
            tenantId,
            tenantContext.UserId,
            "role.created",
            "role",
            role.Id,
            null,
            JsonSerializer.Serialize(new { role.Name, permissions = perms }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(await ToDetailAsync(role, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<RoleDetailDto>> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<RoleDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<RoleDetailDto>(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        if (role.IsSystem)
        {
            return Result.Failure<RoleDetailDto>(
                ErrorCodes.RolesSystemReadonly,
                ErrorCodes.RolesSystemReadonly);
        }

        var targetCheck = await privilegeGuard
            .EnsureCanActOnRolePermissionsAsync(role.Id, cancellationToken)
            .ConfigureAwait(false);
        if (targetCheck.IsFailure)
        {
            return Result.Failure<RoleDetailDto>(targetCheck.ErrorCode!, targetCheck.ErrorMessage!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<RoleDetailDto>(
                ErrorCodes.RolesValidationFailed,
                ErrorCodes.RolesValidationFailed);
        }

        var perms = NormalizePermissions(request.Permissions);
        var grant = await privilegeGuard.EnsureCanGrantAsync(perms, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return Result.Failure<RoleDetailDto>(grant.ErrorCode!, grant.ErrorMessage!);
        }

        var nameClash = await db.Roles.AsNoTracking()
            .AnyAsync(r => r.Id != id && r.Name == request.Name.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (nameClash)
        {
            return Result.Failure<RoleDetailDto>(
                ErrorCodes.RolesValidationFailed,
                ErrorCodes.RolesValidationFailed);
        }

        var oldPerms = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.PermissionCode)
            .OrderBy(c => c)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var oldJson = JsonSerializer.Serialize(new
        {
            role.Name,
            role.DescriptionAr,
            role.DescriptionEn,
            permissions = oldPerms,
        });

        role.UpdateDetails(request.Name, request.DescriptionAr, request.DescriptionEn);

        var existing = await db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.RolePermissions.RemoveRange(existing);
        foreach (var code in perms)
        {
            db.RolePermissions.Add(RolePermission.Create(role.TenantId, role.Id, code));
        }

        audit.Stage(
            role.TenantId,
            tenantContext.UserId,
            "role.updated",
            "role",
            role.Id,
            oldJson,
            JsonSerializer.Serialize(new
            {
                role.Name,
                role.DescriptionAr,
                role.DescriptionEn,
                permissions = perms,
            }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var userIds = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.RoleId == id)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var userId in userIds)
        {
            cacheInvalidator.Invalidate(role.TenantId, userId);
        }

        return Result.Success(await ToDetailAsync(role, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        if (role.IsSystem)
        {
            return Result.Failure(ErrorCodes.RolesSystemReadonly, ErrorCodes.RolesSystemReadonly);
        }

        var targetCheck = await privilegeGuard
            .EnsureCanActOnRolePermissionsAsync(role.Id, cancellationToken)
            .ConfigureAwait(false);
        if (targetCheck.IsFailure)
        {
            return targetCheck;
        }

        var now = DateTimeOffset.UtcNow;
        var inUseByUser = await db.UserRoles.AsNoTracking().AnyAsync(ur => ur.RoleId == id, cancellationToken)
            .ConfigureAwait(false);
        var inUseByInvite = await db.Invitations.AsNoTracking()
            .AnyAsync(
                i => i.RoleId == id
                    && i.AcceptedAt == null
                    && i.RevokedAt == null
                    && i.ExpiresAt > now,
                cancellationToken)
            .ConfigureAwait(false);
        if (inUseByUser || inUseByInvite)
        {
            return Result.Failure(ErrorCodes.RolesInUse, ErrorCodes.RolesInUse);
        }

        var perms = await db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.RolePermissions.RemoveRange(perms);
        db.Roles.Remove(role);
        audit.Stage(
            role.TenantId,
            tenantContext.UserId,
            "role.deleted",
            "role",
            role.Id,
            JsonSerializer.Serialize(new { role.Name }),
            null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public Task<Result<IReadOnlyList<PermissionGroupDto>>> ListPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = PermissionCodes.All
            .GroupBy(code => code.Split('.')[0], StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.OrderBy(c => c, StringComparer.Ordinal)
                    .Select(c => new PermissionItemDto(c, $"permissions.{c}"))
                    .ToList()))
            .ToList();

        IReadOnlyList<PermissionGroupDto> result = groups;
        return Task.FromResult(Result.Success(result));
    }

    private async Task<RoleDetailDto> ToDetailAsync(Role role, CancellationToken cancellationToken)
    {
        var permissions = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionCode)
            .OrderBy(c => c)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userCount = await db.UserRoles.AsNoTracking()
            .CountAsync(ur => ur.RoleId == role.Id, cancellationToken)
            .ConfigureAwait(false);
        return new RoleDetailDto(
            role.Id,
            role.Name,
            role.IsSystem,
            role.DescriptionAr,
            role.DescriptionEn,
            permissions,
            userCount);
    }

    private static List<string> NormalizePermissions(IReadOnlyList<string>? permissions) =>
        (permissions ?? [])
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(p => p, StringComparer.Ordinal)
        .ToList();
}
