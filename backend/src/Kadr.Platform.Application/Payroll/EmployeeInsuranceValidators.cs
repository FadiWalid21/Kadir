using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll;

public sealed class UpsertEmployeeInsuranceRequestValidator : AbstractValidator<UpsertEmployeeInsuranceRequest>
{
    public UpsertEmployeeInsuranceRequestValidator()
    {
        RuleFor(x => x.InsuranceNumber).MaximumLength(64)
            .When(x => x.InsuranceNumber is not null)
            .WithErrorCode(ErrorCodes.InsuranceValidationFailed);
        RuleFor(x => x.InsurableWageOverride).GreaterThanOrEqualTo(0)
            .When(x => x.InsurableWageOverride is not null)
            .WithErrorCode(ErrorCodes.InsuranceValidationFailed);
        RuleFor(x => x.InsuranceNumber).NotEmpty()
            .When(x => x.IsInsured)
            .WithErrorCode(ErrorCodes.InsuranceValidationFailed);
        RuleFor(x => x.InsuranceStartDate).NotEmpty()
            .When(x => x.IsInsured)
            .WithErrorCode(ErrorCodes.InsuranceValidationFailed);
    }
}
