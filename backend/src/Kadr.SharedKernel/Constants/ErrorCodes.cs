namespace Kadr.SharedKernel.Constants;

public static class ErrorCodes
{
    public const string AuthEmailTaken = "auth.email_taken";
    public const string AuthPasswordTooShort = "auth.password_too_short";
    public const string AuthPasswordTooCommon = "auth.password_too_common";
    public const string AuthValidationFailed = "auth.validation_failed";
    public const string IdempotencyKeyRequired = "idempotency.key_required";
    public const string IdempotencyKeyReuse = "idempotency.key_reuse";
    public const string TenantSlugUnavailable = "tenant.slug_unavailable";
    public const string PlanNotFound = "plan.not_found";
    public const string RegistrationFailed = "auth.registration_failed";
}
