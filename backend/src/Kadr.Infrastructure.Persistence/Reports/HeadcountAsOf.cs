using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;

namespace Kadr.Infrastructure.Persistence.Reports;

/// <summary>
/// Headcount inclusion as of date D (module 06 §5.1). Termination date is interim
/// without EMP-07: ContractEndDate, else UpdatedAt in tenant TZ.
/// </summary>
internal static class HeadcountAsOf
{
    public static IQueryable<Employee> Apply(IQueryable<Employee> query, DateOnly asOf, DateTimeOffset updatedAtCutoffUtc)
    {
        return query.Where(e =>
            e.HireDate <= asOf
            && !(
                e.Status == EmployeeStatus.Terminated
                && (
                    (e.ContractEndDate != null && e.ContractEndDate <= asOf)
                    || (e.ContractEndDate == null && e.UpdatedAt != null && e.UpdatedAt < updatedAtCutoffUtc)
                )));
    }

    /// <summary>UTC instant exclusive upper bound for UpdatedAt on as-of date D (start of D+1 in tenant TZ).</summary>
    public static DateTimeOffset UpdatedAtCutoffUtc(DateOnly asOf, string? timeZoneId) =>
        TenantTimeZones.ToUtc(asOf.AddDays(1), TimeOnly.MinValue, timeZoneId);
}
