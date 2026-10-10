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
        [ErrorCodes.IdempotencyKeyRequired] = ("مفتاح التماثل مطلوب.", "Idempotency key is required."),
        [ErrorCodes.IdempotencyKeyReuse] = ("مفتاح التماثل مستخدم بطلب مختلف.", "Idempotency key was reused with a different request."),
        [ErrorCodes.TenantSlugUnavailable] = ("تعذر إنشاء معرف فريد للشركة.", "Could not allocate a unique company slug."),
        [ErrorCodes.PlanNotFound] = ("خطة التجربة غير متاحة.", "Trial plan is not available."),
        [ErrorCodes.RegistrationFailed] = ("فشل التسجيل.", "Registration failed."),
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
