using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.PlatformPlans;

public sealed class PlatformUpdatePlanRequestValidator : AbstractValidator<PlatformUpdatePlanRequest>
{
    public PlatformUpdatePlanRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(PlatformPlanLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(PlatformPlanLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);

        RuleFor(x => x.EmployeeCap)
            .Must(v => v is null or (>= 1 and <= PlatformPlanLimits.MaxEmployeeCap))
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);

        RuleFor(x => x.UnitPrice)
            .Must(v => v is null || (v >= 0m && v <= PlatformPlanLimits.MaxUnitPrice && decimal.Round(v.Value, 2) == v))
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);

        RuleFor(x => x.MinBilledEmployees)
            .Must(v => v is null or (>= 0 and <= PlatformPlanLimits.MaxMinBilledEmployees))
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);

        RuleFor(x => x.FeaturesJson)
            .Must(json => PlatformPlanFeatures.TryNormalize(json, out _))
            .WithErrorCode(ErrorCodes.PlatformValidationFailed);
    }
}
