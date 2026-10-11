using Kadr.Platform.Application.PlatformTenants;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>
/// Audited platform-admin mutations on a tenant. Every method requires a reason and writes an audit row
/// (actor = platform admin, target = tenant).
/// </summary>
public interface IPlatformTenantCommands
{
    Task<Result> ChangePlanAsync(
        Guid tenantId,
        PlatformChangePlanRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ExtendTrialAsync(
        Guid tenantId,
        PlatformExtendTrialRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SuspendAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ResumeAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> CancelSubscriptionAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformIndustryChangeOutcome>> ChangeIndustryAsync(
        Guid tenantId,
        PlatformChangeIndustryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PlatformIndustryChangeOutcome(
    string OldIndustryCode,
    string NewIndustryCode,
    string Mode,
    int AddedCount,
    int DeactivatedCount);
