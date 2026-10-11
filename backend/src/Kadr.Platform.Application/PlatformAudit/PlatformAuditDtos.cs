using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.PlatformAudit;

public sealed record PlatformAuditLogQuery(
    Guid? TenantId,
    Guid? UserId,
    string? EntityType,
    string? Action,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageSize,
    string? Cursor);

/// <summary>Actor kinds in the global audit feed: a tenant user or a platform admin acting on a tenant.</summary>
public static class PlatformAuditActorKinds
{
    public const string TenantUser = "tenant_user";
    public const string PlatformAdmin = "platform_admin";
}

public sealed record PlatformAuditLogItemDto(
    Guid Id,
    DateTimeOffset At,
    Guid TenantId,
    string? TenantName,
    string? TenantSlug,
    string Action,
    string EntityType,
    Guid? EntityId,
    Guid? UserId,
    string ActorKind,
    string? ActorName,
    string? ActorEmail,
    string? IpAddress,
    string? UserAgent,
    string? OldValuesJson,
    string? NewValuesJson);

public sealed record PlatformAuditLogListResultDto(
    IReadOnlyList<PlatformAuditLogItemDto> Items,
    string? NextCursor);

public static class PlatformAuditLogLimits
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxFilterLength = 100;
}

public sealed class PlatformAuditLogQueryValidator : AbstractValidator<PlatformAuditLogQuery>
{
    public PlatformAuditLogQueryValidator()
    {
        RuleFor(x => x.EntityType).MaximumLength(PlatformAuditLogLimits.MaxFilterLength)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
        RuleFor(x => x.Action).MaximumLength(PlatformAuditLogLimits.MaxFilterLength)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(0)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
    }
}
