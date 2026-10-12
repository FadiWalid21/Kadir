using FluentValidation;
using Kadr.Platform.Domain.Notifications;

namespace Kadr.Platform.Application.Notifications;

public sealed class UpdateNotificationPreferencesRequestValidator
    : AbstractValidator<UpdateNotificationPreferencesRequest>
{
    public UpdateNotificationPreferencesRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().NotEmpty();
        RuleFor(x => x.Items)
            .Must(items => items is null
                           || items.Select(i => i.EventType).Distinct(StringComparer.Ordinal).Count() == items.Count)
            .WithMessage("eventType values must be unique.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.EventType)
                .NotEmpty()
                .Must(NotificationEventTypes.IsKnown)
                .WithMessage("eventType is not a known notification event.");
        });
    }
}
