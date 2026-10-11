using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.PlatformTenants;

internal static class PlatformValidationRules
{
    public static IRuleBuilderOptions<T, string> ReasonRules<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithErrorCode(ErrorCodes.PlatformReasonRequired)
            .Must(r => string.IsNullOrWhiteSpace(r) || r.Trim().Length <= PlatformTenantFilters.MaxReasonLength)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
}

public sealed class PlatformChangePlanRequestValidator : AbstractValidator<PlatformChangePlanRequest>
{
    public PlatformChangePlanRequestValidator()
    {
        RuleFor(x => x.Reason).ReasonRules();
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64).WithErrorCode(ErrorCodes.PlatformValidationFailed);
    }
}

public sealed class PlatformExtendTrialRequestValidator : AbstractValidator<PlatformExtendTrialRequest>
{
    public PlatformExtendTrialRequestValidator()
    {
        RuleFor(x => x.Reason).ReasonRules();
        RuleFor(x => x.Days)
            .Must(d => PlatformTenantFilters.AllowedTrialExtensionDays.Contains(d))
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
    }
}

public sealed class PlatformReasonRequestValidator : AbstractValidator<PlatformReasonRequest>
{
    public PlatformReasonRequestValidator()
    {
        RuleFor(x => x.Reason).ReasonRules();
    }
}

public sealed class PlatformChangeIndustryRequestValidator : AbstractValidator<PlatformChangeIndustryRequest>
{
    public PlatformChangeIndustryRequestValidator()
    {
        RuleFor(x => x.Reason).ReasonRules();
        RuleFor(x => x.IndustryCode)
            .Must(IndustryCodes.IsAllowed).WithErrorCode(ErrorCodes.IndustryInvalid);
        RuleFor(x => x.Mode)
            .Must(TemplateSyncModes.IsValid).WithErrorCode(ErrorCodes.PlatformValidationFailed);
    }
}
