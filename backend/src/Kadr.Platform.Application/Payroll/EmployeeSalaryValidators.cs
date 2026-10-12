using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll;

public static class EmployeeSalaryLimits
{
    public const int MaxReasonLength = 500;
    public const int MaxComponents = 50;
}

public sealed class CreateEmployeeSalaryRequestValidator : AbstractValidator<CreateEmployeeSalaryRequest>
{
    public CreateEmployeeSalaryRequestValidator()
    {
        RuleFor(x => x.EffectiveFrom).NotEmpty()
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.ChangeReason).NotEmpty().MaximumLength(EmployeeSalaryLimits.MaxReasonLength)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.BasicAmount).GreaterThanOrEqualTo(0)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.Currency)
            .Must(c => string.IsNullOrWhiteSpace(c) || c.Trim().Length == 3)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.Components)
            .Must(c => c is null || c.Count <= EmployeeSalaryLimits.MaxComponents)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleForEach(x => x.Components).ChildRules(line =>
        {
            line.RuleFor(l => l.PayComponentId).NotEmpty()
                .WithErrorCode(ErrorCodes.SalaryValidationFailed);
            line.RuleFor(l => l.AmountOrRate).GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        }).When(x => x.Components is not null);
    }
}
