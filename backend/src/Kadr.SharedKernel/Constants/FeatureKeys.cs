namespace Kadr.SharedKernel.Constants;

public static class FeatureKeys
{
    public const string CoreHr = "core_hr";
    public const string Attendance = "attendance";
    public const string Leave = "leave";
    public const string Ess = "ess";
    public const string Shifts = "shifts";
    public const string Workflows = "workflows";
    public const string Reports = "reports";
    public const string Documents = "documents";
    public const string DeviceImport = "device_import";
    public const string CustomFields = "custom_fields";
    public const string Api = "api";
    public const string ComplianceDashboards = "compliance_dashboards";
    public const string PrioritySupport = "priority_support";

    public static readonly IReadOnlyList<string> All =
    [
        CoreHr,
        Attendance,
        Leave,
        Ess,
        Shifts,
        Workflows,
        Reports,
        Documents,
        DeviceImport,
        CustomFields,
        Api,
        ComplianceDashboards,
        PrioritySupport,
    ];

    public static readonly IReadOnlyList<string> StarterAndCore =
    [
        CoreHr,
        Attendance,
        Leave,
        Ess,
    ];

    public static readonly IReadOnlyList<string> Growth =
    [
        CoreHr,
        Attendance,
        Leave,
        Ess,
        Shifts,
        Workflows,
        Reports,
        Documents,
        DeviceImport,
    ];

    public static readonly IReadOnlyList<string> Business =
    [
        CoreHr,
        Attendance,
        Leave,
        Ess,
        Shifts,
        Workflows,
        Reports,
        Documents,
        DeviceImport,
        CustomFields,
        Api,
        ComplianceDashboards,
        PrioritySupport,
    ];
}
