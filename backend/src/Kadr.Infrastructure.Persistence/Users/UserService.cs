using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence.Auth;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Users;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Users;

public sealed class UserService(
    KadrDbContext db,
    ITenantContext tenantContext,
    PrivilegeGuard privilegeGuard,
    IPermissionCacheInvalidator cacheInvalidator,
    IRefreshTokenService refreshTokens,
    IAuditLogWriter audit) : IUserService
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedUsersDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        Guid? roleId,
        bool? active,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<PagedUsersDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (active is not null)
        {
            query = query.Where(u => u.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (roleId is not null)
        {
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.IsActive,
                u.LastLoginAt,
                u.EmployeeId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = users.Select(u => u.Id).ToList();
        var roleRows = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where userIds.Contains(ur.UserId)
                select new
                {
                    ur.UserId,
                    ur.RoleId,
                    RoleName = r.Name,
                    r.IsSystem,
                    ur.DataScope,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var employeeIds = users.Where(u => u.EmployeeId is not null).Select(u => u.EmployeeId!.Value).Distinct().ToList();
        var employeeNames = employeeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Employees.AsNoTracking()
                .Where(e => employeeIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FullNameAr })
                .ToDictionaryAsync(e => e.Id, e => e.FullNameAr, cancellationToken)
                .ConfigureAwait(false);

        var items = users.Select(u =>
            {
                roleByUser.TryGetValue(u.Id, out var role);
                string? employeeName = null;
                if (u.EmployeeId is Guid eid)
                {
                    employeeNames.TryGetValue(eid, out employeeName);
                }

                return new UserListItemDto(
                    u.Id,
                    u.FullName,
                    u.Email,
                    role?.RoleId ?? Guid.Empty,
                    role?.RoleName ?? string.Empty,
                    role?.IsSystem ?? false,
                    u.IsActive,
                    role?.DataScope ?? DataScope.All,
                    u.LastLoginAt,
                    u.EmployeeId,
                    employeeName);
            })
            .ToList();

        return Result.Success(new PagedUsersDto(items, page, pageSize, total));
    }

    public async Task<Result<UserDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<UserDetailDto>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersValidationFailed,
                ErrorCodes.UsersValidationFailed);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var targetCheck = await privilegeGuard
            .EnsureCanActOnUserAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (targetCheck.IsFailure)
        {
            return Result.Failure<UserDetailDto>(targetCheck.ErrorCode!, targetCheck.ErrorMessage!);
        }

        var links = await db.UserRoles.Where(ur => ur.UserId == id).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentRoleId = links.FirstOrDefault()?.RoleId;
        var currentScope = links.FirstOrDefault()?.DataScope ?? DataScope.All;

        var roleChanging = currentRoleId != request.RoleId;
        if (roleChanging)
        {
            var roleExists = await db.Roles.AsNoTracking()
                .AnyAsync(r => r.Id == request.RoleId, cancellationToken)
                .ConfigureAwait(false);
            if (!roleExists)
            {
                return Result.Failure<UserDetailDto>(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
            }

            var assign = await privilegeGuard
                .EnsureCanAssignRoleAsync(request.RoleId, cancellationToken)
                .ConfigureAwait(false);
            if (assign.IsFailure)
            {
                return Result.Failure<UserDetailDto>(assign.ErrorCode!, assign.ErrorMessage!);
            }
        }

        var deactivating = user.IsActive && !request.IsActive;
        var reactivating = !user.IsActive && request.IsActive;

        if (deactivating && tenantContext.UserId == id)
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersSelfDeactivationForbidden,
                ErrorCodes.UsersSelfDeactivationForbidden);
        }

        var lastOwner = await EnsureLastOwnerSafeAsync(
                id,
                currentRoleId,
                request.RoleId,
                request.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
        if (lastOwner.IsFailure)
        {
            return Result.Failure<UserDetailDto>(lastOwner.ErrorCode!, lastOwner.ErrorMessage!);
        }

        var oldFullName = user.FullName;
        var oldJson = JsonSerializer.Serialize(new
        {
            fullName = oldFullName,
            roleId = currentRoleId,
            dataScope = currentScope.ToString(),
            isActive = user.IsActive,
        });

        var nameChanged = !string.Equals(oldFullName, request.FullName.Trim(), StringComparison.Ordinal);
        if (nameChanged)
        {
            user.Rename(request.FullName);
        }

        if (roleChanging || currentScope != request.DataScope)
        {
            if (links.Count == 0)
            {
                db.UserRoles.Add(UserRole.Create(user.TenantId, user.Id, request.RoleId, request.DataScope));
            }
            else
            {
                links[0].ReplaceRole(request.RoleId, request.DataScope);
                if (links.Count > 1)
                {
                    db.UserRoles.RemoveRange(links.Skip(1));
                }
            }
        }

        if (deactivating || reactivating)
        {
            user.SetActive(request.IsActive);
        }

        var actorId = tenantContext.UserId;
        if (deactivating)
        {
            audit.Stage(
                user.TenantId,
                actorId,
                "user.deactivated",
                "user",
                user.Id,
                oldJson,
                JsonSerializer.Serialize(new { isActive = false }));
        }

        if (reactivating)
        {
            audit.Stage(
                user.TenantId,
                actorId,
                "user.reactivated",
                "user",
                user.Id,
                oldJson,
                JsonSerializer.Serialize(new { isActive = true }));
        }

        if (roleChanging)
        {
            audit.Stage(
                user.TenantId,
                actorId,
                "user.role_changed",
                "user",
                user.Id,
                JsonSerializer.Serialize(new { roleId = currentRoleId }),
                JsonSerializer.Serialize(new { roleId = request.RoleId, dataScope = request.DataScope.ToString() }));
        }

        if (nameChanged)
        {
            audit.Stage(
                user.TenantId,
                actorId,
                "user.updated",
                "user",
                user.Id,
                JsonSerializer.Serialize(new { fullName = oldFullName }),
                JsonSerializer.Serialize(new { fullName = user.FullName }));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (deactivating)
        {
            await refreshTokens
                .RevokeAllForUserAsync(user.TenantId, user.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        if (deactivating || reactivating || roleChanging)
        {
            cacheInvalidator.Invalidate(user.TenantId, user.Id);
        }

        return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<UserDetailDto>> SetEmployeeLinkAsync(
        Guid id,
        SetUserEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var targetCheck = await privilegeGuard
            .EnsureCanActOnUserAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (targetCheck.IsFailure)
        {
            return Result.Failure<UserDetailDto>(targetCheck.ErrorCode!, targetCheck.ErrorMessage!);
        }

        var tenantId = tenantContext.TenantId.Value;

        if (request.EmployeeId is null)
        {
            if (user.EmployeeId is null)
            {
                return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
            }

            var previousEmployeeId = user.EmployeeId.Value;
            var previous = await db.Employees
                .SingleOrDefaultAsync(e => e.Id == previousEmployeeId, cancellationToken)
                .ConfigureAwait(false);
            if (previous is not null && previous.UserId == user.Id)
            {
                previous.LinkUser(null);
            }

            user.SetEmployeeId(null);
            await audit.WriteAsync(
                    tenantId,
                    tenantContext.UserId,
                    "users.employee_unlinked",
                    "user",
                    user.Id,
                    JsonSerializer.Serialize(new { employeeId = previousEmployeeId }),
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
        }

        var employeeId = request.EmployeeId.Value;
        if (user.EmployeeId is Guid current && current != employeeId)
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersEmployeeAlreadyLinked,
                ErrorCodes.UsersEmployeeAlreadyLinked);
        }

        if (user.EmployeeId == employeeId)
        {
            return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
        }

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<UserDetailDto>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        if (employee.Status is not (EmployeeStatus.Active or EmployeeStatus.Probation))
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersEmployeeInactive,
                ErrorCodes.UsersEmployeeInactive);
        }

        if (employee.UserId is Guid otherUser && otherUser != user.Id)
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersEmployeeLinkConflict,
                ErrorCodes.UsersEmployeeLinkConflict);
        }

        var otherUserWithEmployee = await db.Users
            .AnyAsync(u => u.Id != user.Id && u.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (otherUserWithEmployee)
        {
            return Result.Failure<UserDetailDto>(
                ErrorCodes.UsersEmployeeLinkConflict,
                ErrorCodes.UsersEmployeeLinkConflict);
        }

        user.SetEmployeeId(employeeId);
        employee.LinkUser(user.Id);
        await audit.WriteAsync(
                tenantId,
                tenantContext.UserId,
                "users.employee_linked",
                "user",
                user.Id,
                null,
                JsonSerializer.Serialize(new { employeeId }),
                cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await ToDetailAsync(user.Id, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result> EnsureLastOwnerSafeAsync(
        Guid userId,
        Guid? currentRoleId,
        Guid newRoleId,
        bool willBeActive,
        CancellationToken cancellationToken)
    {
        if (currentRoleId is null)
        {
            return Result.Success();
        }

        var ownerRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.Name == SystemRoleNames.Owner)
            .Select(r => r.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (ownerRoleId == Guid.Empty || currentRoleId != ownerRoleId)
        {
            return Result.Success();
        }

        var userIsActiveOwner = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (!userIsActiveOwner)
        {
            return Result.Success();
        }

        var otherActiveOwners = await (
                from ur in db.UserRoles.AsNoTracking()
                join u in db.Users.AsNoTracking() on ur.UserId equals u.Id
                where ur.RoleId == ownerRoleId && u.IsActive && u.Id != userId
                select u.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (otherActiveOwners)
        {
            return Result.Success();
        }

        var demoting = newRoleId != ownerRoleId;
        var deactivating = !willBeActive;
        if (demoting || deactivating)
        {
            return Result.Failure(ErrorCodes.UsersLastOwnerProtected, ErrorCodes.UsersLastOwnerProtected);
        }

        return Result.Success();
    }

    private async Task<UserDetailDto> ToDetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt,
                u.EmployeeId,
            })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var role = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.UserId == userId
                select new
                {
                    ur.RoleId,
                    RoleName = r.Name,
                    r.IsSystem,
                    ur.DataScope,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? employeeName = null;
        if (user.EmployeeId is Guid eid)
        {
            employeeName = await db.Employees.AsNoTracking()
                .Where(e => e.Id == eid)
                .Select(e => e.FullNameAr)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new UserDetailDto(
            user.Id,
            user.FullName,
            user.Email,
            role?.RoleId ?? Guid.Empty,
            role?.RoleName ?? string.Empty,
            role?.IsSystem ?? false,
            user.IsActive,
            role?.DataScope ?? DataScope.All,
            user.LastLoginAt,
            user.CreatedAt,
            user.EmployeeId,
            employeeName);
    }
}
