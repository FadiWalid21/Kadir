using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Attendance;

public sealed class ProxyPunchRequestValidator : AbstractValidator<ProxyPunchRequest>
{
    public const int ReasonMaxLength = 500;

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
    }
}
