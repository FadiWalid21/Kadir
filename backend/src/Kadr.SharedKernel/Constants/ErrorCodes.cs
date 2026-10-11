namespace Kadr.SharedKernel.Constants;

public static class ErrorCodes
{
    public const string AuthEmailTaken = "auth.email_taken";
    public const string AuthPasswordTooShort = "auth.password_too_short";
    public const string AuthPasswordTooCommon = "auth.password_too_common";
    public const string AuthValidationFailed = "auth.validation_failed";
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthEmailNotVerified = "auth.email_not_verified";
    public const string AuthAccountLocked = "auth.account_locked";
    public const string AuthCsrfHeaderRequired = "auth.csrf_header_required";
    public const string AuthRefreshInvalid = "auth.refresh_invalid";
    public const string AuthRefreshReuse = "auth.refresh_reuse";
    public const string AuthVerificationInvalid = "auth.verification_invalid";
    public const string IdempotencyKeyRequired = "idempotency.key_required";
    public const string IdempotencyKeyReuse = "idempotency.key_reuse";
    public const string TenantSlugUnavailable = "tenant.slug_unavailable";
    public const string PlanNotFound = "plan.not_found";
    public const string RegistrationFailed = "auth.registration_failed";
    public const string IndustryInvalid = "industry.invalid";
    public const string FileTooLarge = "file.too_large";
    public const string FileInvalidType = "file.invalid_type";
    public const string TenantNotFound = "tenant.not_found";
    public const string FileNotFound = "file.not_found";
    public const string TenantSettingsValidationFailed = "tenant.settings.validation_failed";
    public const string TemplateSourceMissing = "template.source_missing";
    public const string TemplateChangeNotAllowed = "template.change_not_allowed";
    public const string TemplateValidationFailed = "template.validation_failed";
    public const string TemplateSectionInvalid = "template.section_invalid";
    public const string TemplateItemNotFound = "template.item_not_found";
    public const string UsersPrivilegeEscalation = "users.privilege_escalation";
    public const string UsersLastOwnerProtected = "users.last_owner_protected";
    public const string UsersSelfDeactivationForbidden = "users.self_deactivation_forbidden";
    public const string UsersNotFound = "users.not_found";
    public const string UsersValidationFailed = "users.validation_failed";
    public const string RolesPrivilegeEscalation = "roles.privilege_escalation";
    public const string RolesSystemReadonly = "roles.system_readonly";
    public const string RolesInUse = "roles.in_use";
    public const string RolesNotFound = "roles.not_found";
    public const string RolesValidationFailed = "roles.validation_failed";
    public const string InvitationInvalid = "invitation.invalid";
    public const string InvitationExpired = "invitation.expired";
    public const string InvitationUsed = "invitation.used";
    public const string InvitationRevoked = "invitation.revoked";
    public const string InvitationNotFound = "invitation.not_found";
}
