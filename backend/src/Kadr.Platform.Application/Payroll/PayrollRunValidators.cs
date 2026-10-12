using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll;

public sealed class CreatePayrollRunRequestValidator : AbstractValidator<CreatePayrollRunRequest>
{
    public CreatePayrollRunRequestValidator()
    {
        RuleFor(x => x.PeriodEnd)
            .GreaterThanOrEqualTo(x => x.PeriodStart)
            .WithErrorCode(ErrorCodes.PayrollRunValidationFailed)
            .WithMessage("Period end must be on or after period start.");
    }
}

public sealed class ReopenPayrollRunRequestValidator : AbstractValidator<ReopenPayrollRunRequest>
{
    public ReopenPayrollRunRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithErrorCode(ErrorCodes.PayrollRunValidationFailed);
    }
}

public sealed class HoldPayrollEntryRequestValidator : AbstractValidator<HoldPayrollEntryRequest>
{
    public HoldPayrollEntryRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithErrorCode(ErrorCodes.PayrollRunValidationFailed);
    }
}
