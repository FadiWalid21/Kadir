using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Tokens;
using Kadr.Identity.Domain.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Tenants;
using Kadr.Platform.Domain.Org;
using Kadr.Platform.Domain.Settings;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Results;
using Kadr.Infrastructure.Persistence.Users;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Commands;

public sealed class RegisterTenantCommandHandler(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IPasswordHasher passwordHasher,
    ICommonPasswordList commonPasswords,
    IEmailSender emailSender,
    IEmailUniquenessChecker emailUniqueness,
    IRegisterTenantFailureInjector failureInjector,
    ITemplateApplyService templateApply,
    ILogger<RegisterTenantCommandHandler> logger)
    : ICommandHandler<RegisterTenantCommand, RegisterTenantResult>
{
    private static readonly TimeSpan TrialDuration = TimeSpan.FromDays(14);
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromDays(2);
    private const int MaxSlugAttempts = 8;

    public async Task<Result<RegisterTenantResult>> HandleAsync(
        RegisterTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        var passwordError = PasswordRules.Validate(command.Password, commonPasswords.Passwords);
        if (passwordError is not null)
        {
            return Result.Failure<RegisterTenantResult>(passwordError, passwordError);
        }

        if (!IndustryCodes.IsAllowed(command.IndustryCode))
        {
            return Result.Failure<RegisterTenantResult>(ErrorCodes.IndustryInvalid, ErrorCodes.IndustryInvalid);
        }

        var email = EmailNormalizer.ForDisplay(command.Email);
        var normalizedEmail = EmailNormalizer.Normalize(command.Email);
        var language = string.Equals(command.DefaultLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? TenantLanguage.En
            : TenantLanguage.Ar;
        var preferredLanguage = language == TenantLanguage.En ? "en" : "ar";
        var timeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? "Africa/Cairo" : command.TimeZone.Trim();
        var country = string.IsNullOrWhiteSpace(command.CountryCode)
            ? "EG"
            : command.CountryCode.Trim().ToUpperInvariant();

        var plan = await db.Plans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Code == PlanCodes.TrialDefault && p.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            return Result.Failure<RegisterTenantResult>(ErrorCodes.PlanNotFound, ErrorCodes.PlanNotFound);
        }

        var tenantId = GuidV7.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var trialEndsAt = now.Add(TrialDuration);
        var baseSlug = TenantSlug.FromName(command.CompanyName);
        var slug = await AllocateSlugAsync(baseSlug, cancellationToken).ConfigureAwait(false);
        if (slug is null)
        {
            return Result.Failure<RegisterTenantResult>(ErrorCodes.TenantSlugUnavailable, ErrorCodes.TenantSlugUnavailable);
        }

        var passwordHash = passwordHasher.HashPassword(command.Password);
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var tenant = Tenant.Create(
                tenantId,
                command.CompanyName,
                slug,
                command.IndustryCode,
                language,
                timeZone,
                trialEndsAt);

            // Subscription is ITenantOwned — ambient tenant must be set before SaveChanges.
            tenantContext.Set(tenantId);
            db.Tenants.Add(tenant);
            db.Subscriptions.Add(Subscription.CreateTrial(tenantId, plan.Id, now, trialEndsAt));

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            failureInjector.AfterTenantInserted();

            var emailCheck = await emailUniqueness
                .EnsureAvailableAsync(tenantId, normalizedEmail, cancellationToken)
                .ConfigureAwait(false);
            if (!emailCheck.IsSuccess)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                tenantContext.Clear();
                return Result.Failure<RegisterTenantResult>(
                    emailCheck.ErrorCode!,
                    emailCheck.ErrorMessage!);
            }

            var roles = CreateSystemRoles(tenantId);
            foreach (var role in roles)
            {
                db.Roles.Add(role);
                foreach (var permission in SystemRolePermissions.ForRole(role.Name))
                {
                    db.RolePermissions.Add(RolePermission.Create(tenantId, role.Id, permission));
                }
            }

            var owner = AppUser.CreateOwner(
                tenantId,
                email,
                normalizedEmail,
                passwordHash,
                command.FullName,
                preferredLanguage);

            db.Users.Add(owner);

            var ownerRole = roles.Single(r => r.Name == SystemRoleNames.Owner);
            db.UserRoles.Add(UserRole.Create(tenantId, owner.Id, ownerRole.Id));

            var legalEntity = LegalEntity.CreateDefault(tenantId, command.CompanyName, country);
            db.LegalEntities.Add(legalEntity);

            var branchName = string.IsNullOrWhiteSpace(command.FirstBranchName)
                ? command.CompanyName
                : command.FirstBranchName.Trim();
            db.Branches.Add(Branch.CreateDefault(tenantId, legalEntity.Id, branchName));

            SeedDefaultSettings(tenantId, country, command);

            db.EmailVerificationTokens.Add(
                EmailVerificationToken.Create(tenantId, owner.Id, tokenHash, now.Add(VerificationLifetime)));

            await templateApply.ApplyAsync(tenantId, command.IndustryCode, cancellationToken)
                .ConfigureAwait(false);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            tenantContext.Set(tenantId, owner.Id);

            try
            {
                await emailSender.SendAsync(
                    new EmailMessage(
                        email,
                        "Verify your Kadr email / تأكيد البريد",
                        $"Verify your email with token: {rawToken}"),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to send verification email for tenant {TenantId} user {UserId}",
                    tenantId,
                    owner.Id);
            }

            logger.LogInformation(
                "Registered tenant {TenantId} with owner {UserId}",
                tenantId,
                owner.Id);

            return Result.Success(new RegisterTenantResult(tenantId, slug, trialEndsAt));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            tenantContext.Clear();
            return Result.Failure<RegisterTenantResult>(ErrorCodes.AuthEmailTaken, ErrorCodes.AuthEmailTaken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            tenantContext.Clear();
            throw;
        }
    }

    private async Task<string?> AllocateSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            var exists = await db.Tenants.AsNoTracking()
                .AnyAsync(t => t.Slug == candidate, cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                return candidate;
            }

            candidate = TenantSlug.WithSuffix(baseSlug, GuidV7.NewGuid());
        }

        return null;
    }

    private static List<Role> CreateSystemRoles(Guid tenantId) =>
    [
        Role.CreateSystem(tenantId, SystemRoleNames.Owner, "المالك", "Owner"),
        Role.CreateSystem(tenantId, SystemRoleNames.HrAdmin, "مسؤول الموارد البشرية", "HR Admin"),
        Role.CreateSystem(tenantId, SystemRoleNames.Manager, "مدير", "Manager"),
        Role.CreateSystem(tenantId, SystemRoleNames.Employee, "موظف", "Employee"),
        Role.CreateSystem(tenantId, SystemRoleNames.Accountant, "محاسب", "Accountant"),
    ];

    private void SeedDefaultSettings(Guid tenantId, string country, RegisterTenantCommand command)
    {
        var workDays = NormalizeDays(
            command.WorkWeekDays,
            ["sun", "mon", "tue", "wed", "thu"]);
        var weekend = NormalizeDays(
            command.WorkWeekWeekend,
            ["fri", "sat"]);
        var start = string.IsNullOrWhiteSpace(command.DefaultWorkStart) ? "09:00" : command.DefaultWorkStart.Trim();
        var end = string.IsNullOrWhiteSpace(command.DefaultWorkEnd) ? "17:00" : command.DefaultWorkEnd.Trim();

        db.TenantSettings.AddRange(
            TenantSetting.Create(tenantId, SettingKeys.CompanyCountry, JsonSerializer.Serialize(country)),
            TenantSetting.Create(tenantId, SettingKeys.WorkWeekDays, JsonSerializer.Serialize(workDays)),
            TenantSetting.Create(tenantId, SettingKeys.WorkWeekWeekend, JsonSerializer.Serialize(weekend)),
            TenantSetting.Create(tenantId, SettingKeys.WorkDayStart, JsonSerializer.Serialize(start)),
            TenantSetting.Create(tenantId, SettingKeys.WorkDayEnd, JsonSerializer.Serialize(end)),
            TenantSetting.Create(tenantId, SettingKeys.DateFormat, JsonSerializer.Serialize("yyyy-MM-dd")),
            TenantSetting.Create(tenantId, SettingKeys.CalendarHijriEnabled, "false"));
    }

    private static string[] NormalizeDays(IReadOnlyList<string>? days, string[] fallback) =>
        days is { Count: > 0 }
            ? days.Select(d => d.Trim().ToLowerInvariant()).Distinct().ToArray()
            : fallback;

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
