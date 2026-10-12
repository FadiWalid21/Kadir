using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Templates;

public static class TemplateItemLimits
{
    public const int MaxNameLength = 200;
}

public sealed class UpdateShiftTemplateRequestValidator : AbstractValidator<UpdateShiftTemplateRequest>
{
    private static readonly Regex TimeRegex = new(
        @"^([01]\d|2[0-3]):[0-5]\d$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public UpdateShiftTemplateRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.StartTime).NotEmpty().Must(t => TimeRegex.IsMatch(t))
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.EndTime).NotEmpty().Must(t => TimeRegex.IsMatch(t))
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.GraceMinutes).GreaterThanOrEqualTo(0)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
    }
}

public sealed class UpdateLeaveTypeTemplateRequestValidator : AbstractValidator<UpdateLeaveTypeTemplateRequest>
{
    public UpdateLeaveTypeTemplateRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.DaysPerYear)
            .Must(v => v is null or >= 0)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
    }
}

public sealed class UpdateDocumentTypeTemplateRequestValidator : AbstractValidator<UpdateDocumentTypeTemplateRequest>
{
    public UpdateDocumentTypeTemplateRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.AlertDays)
            .Must(days => days is null || days.All(d => d >= 0))
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
    }
}

public sealed class UpdateNamedTemplateRequestValidator : AbstractValidator<UpdateNamedTemplateRequest>
{
    public UpdateNamedTemplateRequestValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(TemplateItemLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.TemplateValidationFailed);
    }
}

public static class TemplateTimeParsing
{
    public static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(
            value,
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
}
