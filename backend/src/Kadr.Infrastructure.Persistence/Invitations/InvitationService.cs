using System.Text.Json;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Invitations;
using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Users;
using Kadr.Infrastructure.Persistence.Auth;
using Kadr.Infrastructure.Persistence.Notifications;
using Kadr.Infrastructure.Persistence.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Email;
using Kadr.Platform.Application.Invitations;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Application.Tenants;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Invitations;

public sealed class InvitationService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantAccessor,
    PrivilegeGuard privilegeGuard,
    IEmailUniquenessChecker emailUniqueness,
    IPasswordHasher passwordHasher,
    ICommonPasswordList commonPasswords,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    IAuditLogWriter audit,
    IClock clock,
    INotificationRealtimeNotifier realtime,
    ILogger<InvitationService> logger) : IInvitationService
{
    public async Task<Result<InvitationListItemDto>> InviteAsync(
        InviteUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<InvitationListItemDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Result.Failure<InvitationListItemDto>(
                ErrorCodes.UsersValidationFailed,
                ErrorCodes.UsersValidationFailed);
        }

        var email = request.Email.Trim();
        var normalized = email.ToUpperInvariant();
        var tenantId = tenantContext.TenantId.Value;

        var uniqueness = await emailUniqueness
            .EnsureAvailableAsync(tenantId, normalized, cancellationToken)
            .ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure<InvitationListItemDto>(uniqueness.ErrorCode!, uniqueness.ErrorMessage!);
        }

        var role = await db.Roles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            .ConfigureAwait(false);
        if (role is null)
        {
            return Result.Failure<InvitationListItemDto>(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        var assign = await privilegeGuard
            .EnsureCanAssignRoleAsync(request.RoleId, cancellationToken)
            .ConfigureAwait(false);
        if (assign.IsFailure)
        {
            return Result.Failure<InvitationListItemDto>(assign.ErrorCode!, assign.ErrorMessage!);
        }

        var now = clock.UtcNow;
        var pending = await db.Invitations
            .Where(i =>
                i.NormalizedEmail == normalized
                && i.AcceptedAt == null
                && i.RevokedAt == null
                && i.ExpiresAt > now)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string rawToken;
        Invitation invitation;
        if (pending is not null)
        {
            rawToken = TokenHashing.CreateOpaqueToken();
            pending.Resend(TokenHashing.Hash(rawToken), now.Add(AuthConstants.InvitationLifetime), request.RoleId);
            invitation = pending;
        }
        else
        {
            rawToken = TokenHashing.CreateOpaqueToken();
            invitation = Invitation.Create(
                tenantId,
                email,
                normalized,
                request.RoleId,
                TokenHashing.Hash(rawToken),
                now.Add(AuthConstants.InvitationLifetime),
                tenantContext.UserId);
            db.Invitations.Add(invitation);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantId,
                tenantContext.UserId,
                pending is null ? "invitation.created" : "invitation.resent",
                "invitation",
                invitation.Id,
                null,
                JsonSerializer.Serialize(new { email, roleId = request.RoleId }),
                cancellationToken)
            .ConfigureAwait(false);

        var acceptUrl = BuildAcceptUrl(rawToken);
        await SendInviteEmailAsync(email, rawToken, acceptUrl, cancellationToken).ConfigureAwait(false);
        return Result.Success(
            await ToListItemAsync(invitation, acceptUrl, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<InvitationListItemDto>>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<IReadOnlyList<InvitationListItemDto>>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        var now = clock.UtcNow;
        var rows = await (
                from i in db.Invitations.AsNoTracking()
                join r in db.Roles.AsNoTracking() on i.RoleId equals r.Id
                where i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now
                orderby i.CreatedAt descending
                select new InvitationListItemDto(i.Id, i.Email, i.RoleId, r.Name, i.ExpiresAt, i.CreatedAt, null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<InvitationListItemDto> result = rows;
        return Result.Success(result);
    }

    public async Task<Result<InvitationListItemDto>> ResendAsync(
        Guid id,
        Guid? roleId,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<InvitationListItemDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null
            || invitation.AcceptedAt is not null
            || invitation.RevokedAt is not null)
        {
            return Result.Failure<InvitationListItemDto>(
                ErrorCodes.InvitationNotFound,
                ErrorCodes.InvitationNotFound);
        }

        var newRoleId = roleId ?? invitation.RoleId;
        var roleExists = await db.Roles.AsNoTracking()
            .AnyAsync(r => r.Id == newRoleId, cancellationToken)
            .ConfigureAwait(false);
        if (!roleExists)
        {
            return Result.Failure<InvitationListItemDto>(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        var assign = await privilegeGuard
            .EnsureCanAssignRoleAsync(newRoleId, cancellationToken)
            .ConfigureAwait(false);
        if (assign.IsFailure)
        {
            return Result.Failure<InvitationListItemDto>(assign.ErrorCode!, assign.ErrorMessage!);
        }

        var rawToken = TokenHashing.CreateOpaqueToken();
        invitation.Resend(TokenHashing.Hash(rawToken), clock.UtcNow.Add(AuthConstants.InvitationLifetime), newRoleId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                invitation.TenantId,
                tenantContext.UserId,
                "invitation.resent",
                "invitation",
                invitation.Id,
                null,
                JsonSerializer.Serialize(new { invitation.Email, roleId = newRoleId }),
                cancellationToken)
            .ConfigureAwait(false);

        var acceptUrl = BuildAcceptUrl(rawToken);
        await SendInviteEmailAsync(invitation.Email, rawToken, acceptUrl, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(
            await ToListItemAsync(invitation, acceptUrl, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (invitation is null || invitation.AcceptedAt is not null)
        {
            return Result.Failure(ErrorCodes.InvitationNotFound, ErrorCodes.InvitationNotFound);
        }

        if (invitation.RevokedAt is not null)
        {
            return Result.Success();
        }

        invitation.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                invitation.TenantId,
                tenantContext.UserId,
                "invitation.revoked",
                "invitation",
                invitation.Id,
                null,
                JsonSerializer.Serialize(new { invitation.Email }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<ValidateInvitationResponse>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Success(new ValidateInvitationResponse("invalid", null, null, null));
        }

        var hash = TokenHashing.Hash(token.Trim());
        var invitation = await db.Invitations.AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return Result.Success(new ValidateInvitationResponse("invalid", null, null, null));
        }

        if (invitation.AcceptedAt is not null)
        {
            return Result.Success(new ValidateInvitationResponse("used", null, null, null));
        }

        if (invitation.RevokedAt is not null)
        {
            return Result.Success(new ValidateInvitationResponse("revoked", null, null, null));
        }

        if (invitation.ExpiresAt <= clock.UtcNow)
        {
            return Result.Success(new ValidateInvitationResponse("expired", null, null, null));
        }

        var companyName = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == invitation.TenantId)
            .Select(t => t.Name)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        var roleName = await db.Roles.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.Id == invitation.RoleId)
            .Select(r => r.Name)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ValidateInvitationResponse("valid", invitation.Email, companyName, roleName));
    }

    public async Task<Result> AcceptAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result.Failure(ErrorCodes.InvitationInvalid, ErrorCodes.InvitationInvalid);
        }

        var passwordError = PasswordRules.Validate(request.Password, commonPasswords.Passwords);
        if (passwordError is not null)
        {
            return Result.Failure(passwordError, passwordError);
        }

        var hash = TokenHashing.Hash(request.Token.Trim());
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var invitation = await db.Invitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return Result.Failure(ErrorCodes.InvitationInvalid, ErrorCodes.InvitationInvalid);
        }

        if (invitation.AcceptedAt is not null)
        {
            return Result.Failure(ErrorCodes.InvitationUsed, ErrorCodes.InvitationUsed);
        }

        if (invitation.RevokedAt is not null)
        {
            return Result.Failure(ErrorCodes.InvitationRevoked, ErrorCodes.InvitationRevoked);
        }

        if (invitation.ExpiresAt <= clock.UtcNow)
        {
            return Result.Failure(ErrorCodes.InvitationExpired, ErrorCodes.InvitationExpired);
        }

        var roleExists = await db.Roles.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Id == invitation.RoleId && r.TenantId == invitation.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!roleExists)
        {
            return Result.Failure(ErrorCodes.RolesNotFound, ErrorCodes.RolesNotFound);
        }

        tenantAccessor.Set(invitation.TenantId);

        var uniqueness = await emailUniqueness
            .EnsureAvailableAsync(invitation.TenantId, invitation.NormalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (uniqueness.IsFailure)
        {
            return Result.Failure(uniqueness.ErrorCode!, uniqueness.ErrorMessage!);
        }

        var claimed = await db.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.Id == invitation.Id && i.AcceptedAt == null && i.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.AcceptedAt, clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        if (claimed != 1)
        {
            return Result.Failure(ErrorCodes.InvitationUsed, ErrorCodes.InvitationUsed);
        }

        var user = AppUser.CreateInvited(
            invitation.TenantId,
            invitation.Email,
            invitation.NormalizedEmail,
            passwordHasher.HashPassword(request.Password),
            request.FullName,
            "ar");
        db.Users.Add(user);
        db.UserRoles.Add(UserRole.Create(invitation.TenantId, user.Id, invitation.RoleId));

        // Auto-link when exactly one unmatched employee shares this email (case-insensitive).
        var emailMatches = await db.Employees
            .Where(e => e.UserId == null && e.Email != null && e.Email.ToUpper() == invitation.NormalizedEmail)
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (emailMatches.Count == 1)
        {
            var employee = emailMatches[0];
            user.SetEmployeeId(employee.Id);
            employee.LinkUser(user.Id);
        }

        // Suppress auto AppUser create audit that would include PasswordHash (redacted anyway);
        // keep a single invitation.accepted row keyed to the new user id.
        audit.Stage(
            invitation.TenantId,
            user.Id,
            "invitation.accepted",
            "invitation",
            user.Id,
            null,
            JsonSerializer.Serialize(new
            {
                userId = user.Id,
                invitation.Email,
                invitation.RoleId,
                invitationId = invitation.Id,
                employeeId = user.EmployeeId,
            }));

        if (invitation.CreatedBy is { } inviterId && inviterId != Guid.Empty)
        {
            await NotificationCreator.CreateAsync(
                    db,
                    realtime,
                    invitation.TenantId,
                    inviterId,
                    NotificationContentBuilder.InvitationAccepted(invitation.Email, request.FullName),
                    NotificationLinkPaths.InvitationsSettings,
                    JsonSerializer.Serialize(new
                    {
                        invitationId = invitation.Id,
                        userId = user.Id,
                        invitation.Email,
                    }),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private string? BuildAcceptUrl(string rawToken)
    {
        var url = PublicWebLinks.TryBuild(
            appOptions.Value.PublicWebBaseUrl,
            PublicWebLinks.AcceptInvitationPath,
            rawToken);
        if (url is null)
        {
            logger.LogWarning(
                "App:PublicWebBaseUrl is empty; invitation email will not include an accept link");
        }

        return url;
    }

    private async Task SendInviteEmailAsync(
        string email,
        string rawToken,
        string? acceptUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailSender
                .SendAsync(AuthEmailBodies.Invitation(email, acceptUrl, rawToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send invitation email to {Email}", email);
        }
    }

    private async Task<InvitationListItemDto> ToListItemAsync(
        Invitation invitation,
        string? acceptUrl,
        CancellationToken cancellationToken)
    {
        var roleName = await db.Roles.AsNoTracking()
            .Where(r => r.Id == invitation.RoleId)
            .Select(r => r.Name)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return new InvitationListItemDto(
            invitation.Id,
            invitation.Email,
            invitation.RoleId,
            roleName,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            acceptUrl);
    }
}
