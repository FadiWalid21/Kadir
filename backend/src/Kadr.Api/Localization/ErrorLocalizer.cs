using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Localization;

public sealed class ErrorLocalizer : IErrorLocalizer
{
    private static readonly Dictionary<string, (string Ar, string En)> Messages = new()
    {
        [ErrorCodes.AuthEmailTaken] = ("البريد الإلكتروني مستخدم بالفعل في هذه المنشأة.", "This email is already used in this tenant."),
        [ErrorCodes.AuthPasswordTooShort] = ("كلمة المرور قصيرة جداً.", "Password is too short."),
        [ErrorCodes.AuthPasswordTooCommon] = ("كلمة المرور شائعة جداً.", "Password is too common."),
        [ErrorCodes.AuthValidationFailed] = ("بيانات التسجيل غير صالحة.", "Registration data is invalid."),
        [ErrorCodes.AuthInvalidCredentials] = ("البريد أو كلمة المرور غير صحيحة.", "Email or password is incorrect."),
        [ErrorCodes.AuthEmailNotVerified] = ("يرجى تأكيد البريد الإلكتروني أولاً.", "Please verify your email first."),
        [ErrorCodes.AuthAccountLocked] = ("الحساب مقفل مؤقتاً. حاول لاحقاً.", "Account is temporarily locked. Try again later."),
        [ErrorCodes.AuthCsrfHeaderRequired] = ("رأس الحماية مطلوب.", "CSRF header is required."),
        [ErrorCodes.AuthRefreshInvalid] = ("جلسة التحديث غير صالحة.", "Refresh session is invalid."),
        [ErrorCodes.AuthRefreshReuse] = ("تم إلغاء جلسة التحديث بسبب إعادة استخدام رمز.", "Refresh session revoked due to token reuse."),
        [ErrorCodes.AuthVerificationInvalid] = ("رمز التأكيد غير صالح أو منتهٍ.", "Verification token is invalid or expired."),
        [ErrorCodes.IdempotencyKeyRequired] = ("مفتاح التماثل مطلوب.", "Idempotency key is required."),
        [ErrorCodes.IdempotencyKeyReuse] = ("مفتاح التماثل مستخدم بطلب مختلف.", "Idempotency key was reused with a different request."),
        [ErrorCodes.TenantSlugUnavailable] = ("تعذر إنشاء معرف فريد للشركة.", "Could not allocate a unique company slug."),
        [ErrorCodes.PlanNotFound] = ("خطة التجربة غير متاحة.", "Trial plan is not available."),
        [ErrorCodes.PlanEmployeeCapExceeded] = ("تم تجاوز الحد الأقصى لعدد الموظفين في خطتك.", "Your plan employee limit has been exceeded."),
        [ErrorCodes.PlanFeatureNotAvailable] = ("هذه الميزة غير متاحة في خطتك الحالية.", "This feature is not available on your current plan."),
        [ErrorCodes.SubscriptionExpired] = ("انتهت صلاحية الاشتراك أو التجربة. الحساب للقراءة فقط.", "Subscription or trial has expired. The account is read-only."),
        [ErrorCodes.SubscriptionNotFound] = ("الاشتراك غير موجود.", "Subscription was not found."),
        [ErrorCodes.RegistrationFailed] = ("فشل التسجيل.", "Registration failed."),
        [ErrorCodes.IndustryInvalid] = ("قطاع العمل غير مدعوم.", "Industry is not supported."),
        [ErrorCodes.FileTooLarge] = ("حجم الملف أكبر من 2 ميغابايت.", "File exceeds 2 MB."),
        [ErrorCodes.FileInvalidType] = ("نوع الملف غير مدعوم. استخدم PNG أو JPG أو SVG.", "Unsupported file type. Use PNG, JPG, or SVG."),
        [ErrorCodes.TenantNotFound] = ("المنشأة غير موجودة.", "Tenant was not found."),
        [ErrorCodes.FileNotFound] = ("الملف غير موجود.", "File was not found."),
        [ErrorCodes.TenantSettingsValidationFailed] = ("بيانات إعدادات المنشأة غير صالحة.", "Tenant settings are invalid."),
        [ErrorCodes.TemplateSourceMissing] = ("تعذر العثور على مصدر القالب لإعادة التعيين.", "Template source item was not found for reset."),
        [ErrorCodes.TemplateChangeNotAllowed] = ("لا يمكن تغيير قطاع العمل بعد التسجيل.", "Industry cannot be changed after registration."),
        [ErrorCodes.TemplateValidationFailed] = ("بيانات عنصر القالب غير صالحة.", "Template item data is invalid."),
        [ErrorCodes.TemplateSectionInvalid] = ("قسم القالب غير معروف.", "Template section is invalid."),
        [ErrorCodes.TemplateItemNotFound] = ("عنصر القالب غير موجود.", "Template item was not found."),
        [ErrorCodes.UsersPrivilegeEscalation] = ("لا يمكنك إدارة مستخدم بصلاحيات أعلى من صلاحياتك.", "You cannot manage a user with permissions beyond your own."),
        [ErrorCodes.UsersLastOwnerProtected] = ("لا يمكن تعطيل أو تخفيض آخر مالك نشط.", "The last active Owner cannot be deactivated or demoted."),
        [ErrorCodes.UsersSelfDeactivationForbidden] = ("لا يمكنك تعطيل حسابك بنفسك.", "You cannot deactivate your own account."),
        [ErrorCodes.UsersNotFound] = ("المستخدم غير موجود.", "User was not found."),
        [ErrorCodes.UsersValidationFailed] = ("بيانات المستخدم غير صالحة.", "User data is invalid."),
        [ErrorCodes.RolesPrivilegeEscalation] = ("لا يمكنك منح أو إدارة صلاحيات أعلى من صلاحياتك.", "You cannot grant or manage permissions beyond your own."),
        [ErrorCodes.RolesSystemReadonly] = ("أدوار النظام للقراءة فقط.", "System roles are read-only."),
        [ErrorCodes.RolesInUse] = ("لا يمكن حذف دور مرتبط بمستخدمين أو دعوات معلقة.", "Cannot delete a role that has users or pending invitations."),
        [ErrorCodes.RolesNotFound] = ("الدور غير موجود.", "Role was not found."),
        [ErrorCodes.RolesValidationFailed] = ("بيانات الدور غير صالحة.", "Role data is invalid."),
        [ErrorCodes.InvitationInvalid] = ("رابط الدعوة غير صالح.", "Invitation link is invalid."),
        [ErrorCodes.InvitationExpired] = ("انتهت صلاحية الدعوة.", "Invitation has expired."),
        [ErrorCodes.InvitationUsed] = ("تم استخدام هذه الدعوة مسبقاً.", "Invitation was already used."),
        [ErrorCodes.InvitationRevoked] = ("تم إلغاء هذه الدعوة.", "Invitation was revoked."),
        [ErrorCodes.InvitationNotFound] = ("الدعوة غير موجودة.", "Invitation was not found."),
        [ErrorCodes.AuditExportTooLarge] = (
            "عدد سجلات التدقيق يتجاوز حد التصدير. ضيّق عوامل التصفية.",
            "Audit export exceeds the row limit. Narrow the filters."),
        [ErrorCodes.AuditCursorInvalid] = ("مؤشر الصفحة غير صالح.", "Audit page cursor is invalid."),
    };

    public string GetDetail(string errorCode, string? acceptLanguage)
    {
        var useArabic = IsArabic(acceptLanguage);
        if (Messages.TryGetValue(errorCode, out var pair))
        {
            return useArabic ? pair.Ar : pair.En;
        }

        return useArabic ? "حدث خطأ." : "An error occurred.";
    }

    private static bool IsArabic(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return true;
        }

        var primary = acceptLanguage.Split(',').FirstOrDefault()?.Trim() ?? "ar";
        return primary.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    }
}
