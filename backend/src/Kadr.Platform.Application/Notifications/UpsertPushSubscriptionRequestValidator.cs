using FluentValidation;
using Kadr.Platform.Domain.Notifications;

namespace Kadr.Platform.Application.Notifications;

public sealed class UpsertPushSubscriptionRequestValidator : AbstractValidator<UpsertPushSubscriptionRequest>
{
    public UpsertPushSubscriptionRequestValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .MaximumLength(PushSubscription.MaxEndpointLength);
        RuleFor(x => x.Keys).NotNull();
        RuleFor(x => x.Keys.P256dh).NotEmpty().MaximumLength(PushSubscription.MaxKeyLength).When(x => x.Keys is not null);
        RuleFor(x => x.Keys.Auth).NotEmpty().MaximumLength(PushSubscription.MaxKeyLength).When(x => x.Keys is not null);
        RuleFor(x => x.DeviceLabel).MaximumLength(PushSubscription.MaxDeviceLabelLength).When(x => x.DeviceLabel is not null);
    }
}
