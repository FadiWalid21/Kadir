using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Tenants;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    private static readonly HashSet<string> DayCodes =
    [
        "sun", "mon", "tue", "wed", "thu", "fri", "sat",
    ];

    private static readonly Regex TimeHm = new(
        @"^([01]\d|2[0-3]):[0-5]\d$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);

        // With a Google registration token the identity email comes from the token, so the body email is not required.
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320)
            .When(x => !HasGoogleToken(x));
        RuleFor(x => x.GoogleRegistrationToken).MaximumLength(512);

        // Either a valid password or a Google registration token. A password sent alongside the token must still pass the rules.
        RuleFor(x => x.Password).NotEmpty().When(x => !HasGoogleToken(x));
        RuleFor(x => x.Password!).MinimumLength(PasswordRules.MinimumLength)
            .When(x => !string.IsNullOrEmpty(x.Password));
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IndustryCode)
            .NotEmpty()
            .MaximumLength(64)
            .Must(IndustryCodes.IsAllowed)
            .WithErrorCode(ErrorCodes.IndustryInvalid)
            .WithMessage(ErrorCodes.IndustryInvalid);
        RuleFor(x => x.TimeZone).MaximumLength(64).When(x => x.TimeZone is not null);
        RuleFor(x => x.DefaultLanguage)
            .Must(lang => lang is null or "ar" or "en")
            .WithMessage("defaultLanguage must be ar or en.");
        RuleFor(x => x.CountryCode).MaximumLength(2).When(x => x.CountryCode is not null);
        RuleFor(x => x.FirstBranchName).MaximumLength(200).When(x => x.FirstBranchName is not null);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128).When(x => x.IdempotencyKey is not null);

        RuleFor(x => x.WorkWeekDays)
            .Must(days => days is null || days.Count > 0 && days.All(d => DayCodes.Contains(d.Trim().ToLowerInvariant())))
            .WithMessage("workWeekDays must use sun–sat codes.");
        RuleFor(x => x.WorkWeekWeekend)
            .Must(days => days is null || days.All(d => DayCodes.Contains(d.Trim().ToLowerInvariant())))
            .WithMessage("workWeekWeekend must use sun–sat codes.");

        RuleFor(x => x.DefaultWorkStart)
            .Must(t => t is null || TimeHm.IsMatch(t))
            .WithMessage("defaultWorkStart must be HH:mm.");
        RuleFor(x => x.DefaultWorkEnd)
            .Must(t => t is null || TimeHm.IsMatch(t))
            .WithMessage("defaultWorkEnd must be HH:mm.");

        RuleFor(x => x)
            .Must(x =>
            {
                if (x.DefaultWorkStart is null || x.DefaultWorkEnd is null)
                {
                    return true;
                }

                var start = TimeSpan.ParseExact(x.DefaultWorkStart, @"hh\:mm", CultureInfo.InvariantCulture);
                var end = TimeSpan.ParseExact(x.DefaultWorkEnd, @"hh\:mm", CultureInfo.InvariantCulture);
                return end > start;
            })
            .WithMessage("defaultWorkEnd must be after defaultWorkStart.");
    }

    private static bool HasGoogleToken(RegisterTenantCommand command) =>
        !string.IsNullOrWhiteSpace(command.GoogleRegistrationToken);
}
