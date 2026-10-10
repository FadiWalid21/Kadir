using Kadr.Platform.Application.Tenants;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Commands;

public sealed class UpdateTenantSettingsCommandHandler(
    KadrDbContext db,
    ITenantContext tenantContext) : ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsResult>
{
    public async Task<Result<TenantSettingsResult>> HandleAsync(
        UpdateTenantSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<TenantSettingsResult>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<TenantSettingsResult>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        tenant.Rename(command.Name);

        var language = command.DefaultLanguage is null
            ? tenant.DefaultLanguage
            : ParseLanguage(command.DefaultLanguage);
        var timeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? tenant.TimeZone : command.TimeZone.Trim();
        tenant.UpdateDefaults(language, timeZone);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ToResult(tenant));
    }

    internal static TenantSettingsResult ToResult(Tenant tenant) =>
        new(
            tenant.Name,
            tenant.DefaultLanguage == TenantLanguage.En ? "en" : "ar",
            tenant.TimeZone,
            tenant.LogoFileId,
            tenant.LogoFileId is null ? null : $"/api/v1/files/{tenant.LogoFileId:D}");

    private static TenantLanguage ParseLanguage(string value) =>
        value.Equals("en", StringComparison.OrdinalIgnoreCase) ? TenantLanguage.En : TenantLanguage.Ar;
}
