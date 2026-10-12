using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll;

public sealed class UpsertPayrollRuleRequestValidator : AbstractValidator<UpsertPayrollRuleRequest>
{
    public UpsertPayrollRuleRequestValidator()
    {
        RuleFor(x => x.ValueJson).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveFrom).Must(d => d != default).WithMessage("EffectiveFrom is required.");
    }
}

public sealed class ConfirmPayrollRulesRequestValidator : AbstractValidator<ConfirmPayrollRulesRequest>
{
    public ConfirmPayrollRulesRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public static class PayrollRuleKeyGuard
{
    public static bool IsKnown(string ruleKey) =>
        PayrollRuleKeys.All.Contains(ruleKey, StringComparer.Ordinal);
}
