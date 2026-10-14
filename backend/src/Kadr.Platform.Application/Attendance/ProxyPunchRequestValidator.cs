using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Attendance;

public sealed class ProxyPunchRequestValidator : AbstractValidator<ProxyPunchRequest>
{
    public const int ReasonMaxLength = 500;
    public const int MaxPastDays = 30;
    public const int MaxFutureHours = 24;

    public ProxyPunchRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.AttendanceValidationFailed);

        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.AttendanceValidationFailed);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(ReasonMaxLength)
            .WithErrorCode(ErrorCodes.AttendanceReasonRequired);

        RuleFor(x => x.PunchedAt)
            .Must(BeWithinAllowedWindow)
            .When(x => x.PunchedAt is not null)
            .WithErrorCode(ErrorCodes.AttendanceValidationFailed)
            .WithMessage(ErrorCodes.AttendanceValidationFailed);
    }

    private static bool BeWithinAllowedWindow(DateTimeOffset? punchedAt)
    {
        if (punchedAt is null)
        {
            return true;
        }

        var utc = punchedAt.Value.ToUniversalTime();
        var now = DateTimeOffset.UtcNow;
        if (utc < now.AddDays(-MaxPastDays))
        {
            return false;
        }

        return utc <= now.AddHours(MaxFutureHours);
    }
}
