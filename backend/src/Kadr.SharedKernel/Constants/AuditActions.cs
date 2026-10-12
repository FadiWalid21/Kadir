namespace Kadr.SharedKernel.Constants;

public static class AuditActions
{
    public const string AuthLoginSucceeded = "auth.login_succeeded";
    public const string AuthLoginFailed = "auth.login_failed";
    public const string AuthAccountLocked = "auth.account_locked";
    public const string AuthGoogleLinked = "auth.google_linked";
    public const string AuthGoogleUnlinked = "auth.google_unlinked";
    public const string AuthPasswordAdded = "auth.password_added";
    public const string AuthPasswordChanged = "auth.password_changed";
    public const string AuthPasswordResetRequested = "auth.password_reset_requested";
    public const string AuthPasswordResetCompleted = "auth.password_reset_completed";
    public const string TenantRegisteredWithGoogle = "tenant.registered_with_google";
    public const string ReportExported = "report.exported";
    public const string AttendanceShiftRuleCreate = "attendance.shift_rule.create";
    public const string AttendanceShiftRuleUpdate = "attendance.shift_rule.update";
    public const string AttendanceShiftRuleEnd = "attendance.shift_rule.end";
}
