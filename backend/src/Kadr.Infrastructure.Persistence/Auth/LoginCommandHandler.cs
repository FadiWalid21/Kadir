using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Security;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class LoginCommandHandler(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IPasswordHasher passwordHasher,
    DummyPasswordHash dummyPasswordHash,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IPermissionService permissionService,
    IAuditLogWriter audit,
    IClock clock,
    ILogger<LoginCommandHandler> logger) : ICommandHandler<LoginCommand, LoginResult>
{
    public async Task<Result<LoginResult>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var slug = command.CompanySlug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            _ = passwordHasher.VerifyPassword(dummyPasswordHash.Value, command.Password);
            return Result.Failure<LoginResult>(ErrorCodes.AuthInvalidCredentials, ErrorCodes.AuthInvalidCredentials);
        }

        tenantContext.Set(tenant.Id);

        var normalizedEmail = EmailNormalizer.Normalize(command.Email);
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (user is not null && user.IsLockedOut(clock.UtcNow))
        {
            _ = passwordHasher.VerifyPassword(dummyPasswordHash.Value, command.Password);
            await WriteAuthAuditAsync(
                    tenant.Id,
                    user.Id,
                    AuditActions.AuthAccountLocked,
                    command,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<LoginResult>(ErrorCodes.AuthAccountLocked, ErrorCodes.AuthAccountLocked);
        }

        var hashToVerify = user?.PasswordHash ?? dummyPasswordHash.Value;
        var passwordValid = passwordHasher.VerifyPassword(hashToVerify, command.Password);

        if (user is null || !user.IsActive || !passwordValid)
        {
            if (user is not null && user.IsActive)
            {
                var locked = user.RegisterFailedLogin(clock);
                audit.Stage(
                    tenant.Id,
                    user.Id,
                    AuditActions.AuthLoginFailed,
                    "Auth",
                    user.Id,
                    null,
                    EmailJson(command.Email),
                    command.ClientIp,
                    command.DeviceInfo);
                if (locked)
                {
                    audit.Stage(
                        tenant.Id,
                        user.Id,
                        AuditActions.AuthAccountLocked,
                        "Auth",
                        user.Id,
                        null,
                        EmailJson(command.Email),
                        command.ClientIp,
                        command.DeviceInfo);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                if (locked)
                {
                    logger.LogInformation(
                        "Account locked for user {UserId} tenant {TenantId}",
                        user.Id,
                        user.TenantId);
                    return Result.Failure<LoginResult>(ErrorCodes.AuthAccountLocked, ErrorCodes.AuthAccountLocked);
                }
            }
            else
            {
                await WriteAuthAuditAsync(
                        tenant.Id,
                        null,
                        AuditActions.AuthLoginFailed,
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return Result.Failure<LoginResult>(ErrorCodes.AuthInvalidCredentials, ErrorCodes.AuthInvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            return Result.Failure<LoginResult>(ErrorCodes.AuthEmailNotVerified, ErrorCodes.AuthEmailNotVerified);
        }

        user.RecordSuccessfulLogin(clock);
        audit.Stage(
            tenant.Id,
            user.Id,
            AuditActions.AuthLoginSucceeded,
            "Auth",
            user.Id,
            null,
            EmailJson(command.Email),
            command.ClientIp,
            command.DeviceInfo);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        tenantContext.Set(tenant.Id, user.Id);
        var roles = await permissionService.GetRoleNamesAsync(tenant.Id, user.Id, cancellationToken)
            .ConfigureAwait(false);
        var access = jwtTokenService.CreateAccessToken(user.Id, tenant.Id, roles, user.PreferredLanguage);
        var refresh = await refreshTokenService.IssueAsync(
                tenant.Id,
                user.Id,
                command.ClientIp,
                command.DeviceInfo,
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("User {UserId} logged in for tenant {TenantId}", user.Id, tenant.Id);

        return Result.Success(new LoginResult(
            access.AccessToken,
            access.ExpiresInSeconds,
            refresh.OpaqueToken,
            refresh.ExpiresAt));
    }

    private async Task WriteAuthAuditAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        await audit.WriteAsync(
                tenantId,
                userId,
                action,
                "Auth",
                userId,
                null,
                EmailJson(command.Email),
                cancellationToken,
                command.ClientIp,
                command.DeviceInfo)
            .ConfigureAwait(false);
    }

    private static string EmailJson(string email) =>
        JsonSerializer.Serialize(new { email = EmailNormalizer.Normalize(email) });
}
