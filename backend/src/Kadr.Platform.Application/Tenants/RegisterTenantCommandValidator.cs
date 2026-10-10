using FluentValidation;

namespace Kadr.Platform.Application.Tenants;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(PasswordRules.MinimumLength);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IndustryCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TimeZone).MaximumLength(64).When(x => x.TimeZone is not null);
        RuleFor(x => x.DefaultLanguage)
            .Must(lang => lang is null or "ar" or "en")
            .WithMessage("defaultLanguage must be ar or en.");
        RuleFor(x => x.CountryCode).MaximumLength(2).When(x => x.CountryCode is not null);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128).When(x => x.IdempotencyKey is not null);
    }
}
