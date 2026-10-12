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
        [ErrorCodes.TemplateDuplicateName] = ("يوجد عنصر بنفس الاسم في هذه القائمة.", "An item with the same name already exists in this list."),
        [ErrorCodes.TemplateItemInUse] = ("لا يمكن حذف هذا العنصر لأنه مستخدم. عطّله بدلاً من ذلك.", "Cannot delete this item because it is in use. Disable it instead."),
        [ErrorCodes.TemplateResetNotAvailable] = ("إعادة التعيين غير متاحة للعناصر المخصصة.", "Reset to default is not available for custom items."),
        [ErrorCodes.BranchesValidationFailed] = ("بيانات الفرع غير صالحة.", "Branch data is invalid."),
        [ErrorCodes.BranchesNotFound] = ("الفرع غير موجود.", "Branch was not found."),
        [ErrorCodes.BranchesDuplicateName] = ("يوجد فرع بنفس الاسم.", "A branch with the same name already exists."),
        [ErrorCodes.BranchesLastActive] = ("يجب أن يبقى فرع نشط واحد على الأقل.", "At least one active branch must remain."),
        [ErrorCodes.BranchesInUse] = ("لا يمكن حذف الفرع لأنه مستخدم. عطّله بدلاً من ذلك.", "Cannot delete this branch because it is in use. Disable it instead."),
        [ErrorCodes.BranchesLegalEntityNotFound] = ("الكيان القانوني غير موجود.", "Legal entity was not found."),
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
        [ErrorCodes.AuthGoogleAccountNotFound] = ("لا يوجد حساب مرتبط ببريد جوجل هذا.", "No account is linked to this Google email."),
        [ErrorCodes.AuthGoogleEmailUnverified] = ("بريد جوجل غير مؤكد.", "The Google email is not verified."),
        [ErrorCodes.AuthAccountDisabled] = ("الحساب معطّل.", "The account is disabled."),
        [ErrorCodes.AuthLastSignInMethod] = ("لا يمكن إزالة آخر طريقة لتسجيل الدخول. أضف كلمة مرور أولاً.", "You cannot remove your last sign-in method. Add a password first."),
        [ErrorCodes.AuthGoogleTokenInvalid] = ("بيانات جوجل غير صالحة.", "The Google credential is invalid."),
        [ErrorCodes.AuthGoogleNonceInvalid] = ("انتهت صلاحية طلب جوجل. حاول مرة أخرى.", "The Google request expired. Try again."),
        [ErrorCodes.AuthGoogleSelectionInvalid] = ("انتهت صلاحية اختيار الشركة. سجّل الدخول مرة أخرى.", "The company selection expired. Sign in again."),
        [ErrorCodes.AuthGoogleRegistrationInvalid] = ("انتهت صلاحية التسجيل عبر جوجل. ابدأ من جديد.", "The Google registration expired. Start again."),
        [ErrorCodes.AuthGoogleAlreadyLinked] = ("حساب جوجل مرتبط بالفعل.", "A Google account is already linked."),
        [ErrorCodes.AuthPasswordResetInvalid] = ("رابط إعادة التعيين غير صالح أو منتهٍ.", "The reset link is invalid or expired."),
        [ErrorCodes.AuthGoogleDisabled] = ("تسجيل الدخول عبر جوجل غير مفعّل.", "Google sign-in is not enabled."),
        [ErrorCodes.PlatformReasonRequired] = ("سبب الإجراء مطلوب.", "A reason for this action is required."),
        [ErrorCodes.PlatformValidationFailed] = ("بيانات الطلب غير صالحة.", "The request data is invalid."),
        [ErrorCodes.PlatformTenantStateInvalid] = ("لا يمكن تنفيذ هذا الإجراء على المنشأة في حالتها الحالية.", "This action is not allowed for the tenant in its current state."),
        [ErrorCodes.PlatformNoChange] = ("لا يوجد تغيير: القيمة المطلوبة مطابقة للحالية.", "No change: the requested value matches the current one."),
        [ErrorCodes.PlatformPlanNotFound] = ("الباقة غير موجودة.", "The plan was not found."),
        [ErrorCodes.PlatformPlanProtected] = ("لا يمكن تعطيل الباقة الافتراضية للتجربة المجانية.", "The default trial plan cannot be deactivated."),
        [ErrorCodes.EmployeesNotFound] = ("الموظف غير موجود.", "Employee was not found."),
        [ErrorCodes.EmployeesValidationFailed] = ("بيانات الموظف غير صالحة.", "Employee data is invalid."),
        [ErrorCodes.EmployeesDuplicateNumber] = ("الرقم الوظيفي مستخدم بالفعل.", "Employee number is already used."),
        [ErrorCodes.EmployeesDuplicateNationalId] = ("الرقم القومي مستخدم بالفعل.", "National ID is already used."),
        [ErrorCodes.EmployeesManagerCycle] = ("لا يمكن إنشاء حلقة في سلسلة المديرين.", "Cannot create a manager reporting cycle."),
        [ErrorCodes.EmployeesImportInvalid] = ("ملف الاستيراد غير صالح. استخدم CSV وفق النموذج.", "Import file is invalid. Use the CSV template."),
        [ErrorCodes.EmployeesForbidden] = ("ليست لديك صلاحية لهذا الإجراء على الموظفين.", "You are not allowed to perform this employee action."),
        [ErrorCodes.OrgDepartmentCycle] = ("لا يمكن إنشاء حلقة في هيكل الإدارات.", "Cannot create a department hierarchy cycle."),
        [ErrorCodes.OrgNotFound] = ("عنصر الهيكل غير موجود.", "Org item was not found."),
        [ErrorCodes.AttendanceNoEmployee] = ("حسابك غير مرتبط بملف موظف.", "Your account is not linked to an employee profile."),
        [ErrorCodes.AttendanceEmployeeInactive] = ("الموظف غير نشط ولا يمكن تسجيل الحضور.", "The employee is inactive and cannot punch."),
        [ErrorCodes.AttendanceLocationStale] = ("موقع الجهاز قديم. حدّث الموقع وحاول مرة أخرى.", "Device location is stale. Refresh location and try again."),
        [ErrorCodes.AttendanceLocationInaccurate] = ("دقة الموقع غير كافية للتسجيل.", "Location accuracy is not sufficient to punch."),
        [ErrorCodes.AttendanceBranchNotConfigured] = ("موقع الفرع غير مضبوط بعد.", "Branch location is not configured yet."),
        [ErrorCodes.AttendanceOutsideRange] = ("أنت خارج نطاق الفرع.", "You are outside the branch geofence."),
        [ErrorCodes.AttendanceReasonRequired] = ("مطلوب سبب عند التسجيل من خارج النطاق.", "A reason is required when punching outside the geofence."),
        [ErrorCodes.AttendanceDuplicatePunch] = ("طلب التسجيل مكرر.", "Duplicate punch request."),
        [ErrorCodes.AttendanceValidationFailed] = ("بيانات الحضور غير صالحة.", "Attendance data is invalid."),
        [ErrorCodes.AttendanceNotFound] = ("عنصر الحضور غير موجود.", "Attendance item was not found."),
        [ErrorCodes.AttendanceForbidden] = ("ليست لديك صلاحية لهذا الإجراء على الحضور.", "You are not allowed to perform this attendance action."),
        [ErrorCodes.AttendanceLocationNotFound] = ("موقع العمل غير موجود.", "Work location was not found."),
        [ErrorCodes.AttendanceLocationInactive] = ("موقع العمل غير نشط.", "Work location is inactive."),
        [ErrorCodes.AttendanceAnywhereEvidenceRequired] = ("التسجيل من خارج النطاق يتطلب السبب و/أو الصورة المطلوبة.", "Punching from outside requires the configured reason and/or photo."),
        [ErrorCodes.AttendanceShiftRuleOverlap] = ("توجد قاعدة وردية متداخلة لنفس الهدف والفترة.", "An overlapping shift rule already exists for this target and period."),
        [ErrorCodes.AttendanceShiftRuleValidationFailed] = ("بيانات قاعدة الوردية غير صالحة.", "Shift rule data is invalid."),
        [ErrorCodes.AttendanceShiftRuleNotFound] = ("قاعدة الوردية غير موجودة.", "Shift rule was not found."),
        [ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm] = (
            "القاعدة بأثر رجعي تتطلب تأكيداً. لن تُعاد حساب ملخصات الحضور التاريخية تلقائياً.",
            "A back-dated rule requires confirmation. Historical attendance day summaries are not recalculated automatically."),
        [ErrorCodes.NotificationsNotFound] = ("الإشعار غير موجود.", "Notification was not found."),
        [ErrorCodes.NotificationsValidationFailed] = ("بيانات الإشعارات غير صالحة.", "Notification data is invalid."),
        [ErrorCodes.NotificationsPushNotConfigured] = ("إشعارات الويب غير مهيأة على الخادم.", "Web push is not configured on the server."),
        [ErrorCodes.NotificationsPushEndpointRejected] = ("عنوان اشتراك الإشعار غير مسموح.", "Push subscription endpoint is not allowed."),
        [ErrorCodes.NotificationsPushLimitExceeded] = ("وصلت إلى الحد الأقصى لأجهزة الإشعارات.", "You have reached the maximum number of push devices."),
        [ErrorCodes.NotificationsPushSubscriptionNotFound] = ("اشتراك الإشعار غير موجود.", "Push subscription was not found."),
        [ErrorCodes.ReportsForbidden] = ("ليست لديك صلاحية لعرض التقارير.", "You are not allowed to view reports."),
        [ErrorCodes.ReportsRangeTooLarge] = ("نطاق التاريخ يتجاوز 12 شهراً. ضيّق الفترة.", "The date range exceeds 12 months. Narrow the period."),
        [ErrorCodes.ReportsExportTooLarge] = ("التصدير يتجاوز حد الصفوف. ضيّق عوامل التصفية.", "Export exceeds the row limit. Narrow the filters."),
        [ErrorCodes.ReportsValidationFailed] = ("بيانات التقرير غير صالحة.", "Report request is invalid."),
        [ErrorCodes.ReportsNotFound] = ("التقرير غير موجود.", "Report was not found."),
        [ErrorCodes.SalaryValidationFailed] = ("بيانات الراتب غير صالحة.", "Salary data is invalid."),
        [ErrorCodes.SalaryNotFound] = ("سجل الراتب غير موجود.", "Salary record was not found."),
        [ErrorCodes.SalaryDuplicateKey] = ("يوجد مكون راتب بنفس المفتاح.", "A pay component with the same key already exists."),
        [ErrorCodes.SalaryEmployeeNotFound] = ("الموظف غير موجود.", "Employee was not found."),
        [ErrorCodes.SalaryEffectiveOverlap] = ("تاريخ السريان يتداخل مع راتب سابق. استخدم تاريخاً لاحقاً.", "Effective date overlaps a previous salary. Use a later date."),
        [ErrorCodes.SalaryInvalidComponent] = ("مكون راتب غير صالح أو معطّل أو أساسي.", "Pay component is invalid, inactive, or basic."),
        [ErrorCodes.InsuranceValidationFailed] = ("بيانات التأمين غير صالحة.", "Insurance data is invalid."),
        [ErrorCodes.InsuranceNotFound] = ("ملف التأمين غير موجود.", "Insurance profile was not found."),
        [ErrorCodes.InsuranceEmployeeNotFound] = ("الموظف غير موجود.", "Employee was not found."),
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
