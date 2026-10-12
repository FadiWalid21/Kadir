using System.Text.RegularExpressions;
using FluentValidation;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll;

public static class PayComponentLimits
{
    public const int MaxKeyLength = 64;
    public const int MaxNameLength = 200;
    public static readonly Regex KeyPattern = new("^[a-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}

public sealed class CreatePayComponentRequestValidator : AbstractValidator<CreatePayComponentRequest>
{
    public CreatePayComponentRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(PayComponentLimits.MaxKeyLength)
            .Must(k => PayComponentLimits.KeyPattern.IsMatch(k.Trim().ToLowerInvariant()))
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(PayComponentLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(PayComponentLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.Kind).Must(BeValidKind).WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.CalcType).Must(BeValidCalcType).WithErrorCode(ErrorCodes.SalaryValidationFailed);
    }

    private static bool BeValidKind(string value) =>
        Enum.TryParse<PayComponentKind>(value, ignoreCase: true, out _);

    private static bool BeValidCalcType(string value) =>
        Enum.TryParse<PayComponentCalcType>(value, ignoreCase: true, out _);
}

public sealed class UpdatePayComponentRequestValidator : AbstractValidator<UpdatePayComponentRequest>
{
    public UpdatePayComponentRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(PayComponentLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(PayComponentLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.Kind).Must(BeValidKind).WithErrorCode(ErrorCodes.SalaryValidationFailed);
        RuleFor(x => x.CalcType).Must(BeValidCalcType).WithErrorCode(ErrorCodes.SalaryValidationFailed);
    }

    private static bool BeValidKind(string value) =>
        Enum.TryParse<PayComponentKind>(value, ignoreCase: true, out _);

    private static bool BeValidCalcType(string value) =>
        Enum.TryParse<PayComponentCalcType>(value, ignoreCase: true, out _);
}
