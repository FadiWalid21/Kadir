using Kadr.Identity.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Auth;

/// <summary>Grant and target-privilege checks (roles ⊆ actor permissions).</summary>
public sealed class PrivilegeGuard(KadrDbContext db, IPermissionService permissions, ITenantContext tenantContext)
{
    public async Task<Result> EnsureCanGrantAsync(
        IEnumerable<string> permissionCodes,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var requested = permissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var code in requested)
        {
            if (!PermissionCodes.All.Contains(code))
            {
                return Result.Failure(ErrorCodes.RolesValidationFailed, ErrorCodes.RolesValidationFailed);
            }

            if (!actor.Contains(code))
            {
                return Result.Failure(ErrorCodes.RolesPrivilegeEscalation, ErrorCodes.RolesPrivilegeEscalation);
            }
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanActOnRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var targetPerms = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targetPerms.Any(p => !actor.Contains(p)))
        {
            return Result.Failure(ErrorCodes.RolesPrivilegeEscalation, ErrorCodes.RolesPrivilegeEscalation);
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanActOnUserAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorPermissionsAsync(cancellationToken).ConfigureAwait(false);
        if (actor is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var targetPerms = await (
                from ur in db.UserRoles.AsNoTracking()
                join rp in db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                where ur.UserId == targetUserId
                select rp.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targetPerms.Any(p => !actor.Contains(p)))
        {
            return Result.Failure(ErrorCodes.UsersPrivilegeEscalation, ErrorCodes.UsersPrivilegeEscalation);
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanAssignRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var grant = await EnsureCanActOnRolePermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (grant.IsFailure)
        {
            return Result.Failure(ErrorCodes.UsersPrivilegeEscalation, ErrorCodes.UsersPrivilegeEscalation);
        }

        return Result.Success();
    }

    public async Task<HashSet<string>?> GetActorPermissionsAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return null;
        }

        var list = await permissions
            .GetPermissionsAsync(tenantContext.TenantId.Value, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        return list.ToHashSet(StringComparer.Ordinal);
    }
}
