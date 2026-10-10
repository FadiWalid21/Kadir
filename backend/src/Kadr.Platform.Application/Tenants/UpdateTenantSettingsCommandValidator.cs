using FluentValidation;

namespace Kadr.Platform.Application.Tenants;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultLanguage)
            .Must(lang => lang is null or "ar" or "en")
            .WithMessage("defaultLanguage must be ar or en.");
        RuleFor(x => x.TimeZone).MaximumLength(64).When(x => x.TimeZone is not null);
    }
}
