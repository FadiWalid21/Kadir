using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Tests.Payroll;

public sealed class SystemRolePayrollPermissionsTests
{
    [Fact]
    public void Owner_includes_all_payroll_permissions()
    {
        var perms = SystemRolePermissions.ForRole(SystemRoleNames.Owner);
        Assert.Contains(PermissionCodes.PayrollRulesRead, perms);
        Assert.Contains(PermissionCodes.PayrollRulesManage, perms);
        Assert.Contains(PermissionCodes.PayrollRun, perms);
        Assert.Contains(PermissionCodes.PayrollApprove, perms);
        Assert.Contains(PermissionCodes.PayrollLock, perms);
        Assert.Contains(PermissionCodes.PayrollReopen, perms);
    }

    [Fact]
    public void Accountant_has_rules_read_run_and_approve_only()
    {
        var perms = SystemRolePermissions.ForRole(SystemRoleNames.Accountant);
        Assert.Contains(PermissionCodes.PayrollRulesRead, perms);
        Assert.Contains(PermissionCodes.PayrollRun, perms);
        Assert.Contains(PermissionCodes.PayrollApprove, perms);
        Assert.DoesNotContain(PermissionCodes.PayrollRulesManage, perms);
        Assert.DoesNotContain(PermissionCodes.PayrollLock, perms);
        Assert.DoesNotContain(PermissionCodes.PayrollReopen, perms);
    }

    [Fact]
    public void Employee_has_no_payroll_permissions()
    {
        var perms = SystemRolePermissions.ForRole(SystemRoleNames.Employee);
        Assert.DoesNotContain(PermissionCodes.PayrollRulesRead, perms);
        Assert.DoesNotContain(PermissionCodes.PayrollRun, perms);
        Assert.DoesNotContain(PermissionCodes.PayrollApprove, perms);
    }

    [Fact]
    public void PermissionCodes_All_includes_payroll_codes()
    {
        Assert.Contains(PermissionCodes.PayrollRulesRead, PermissionCodes.All);
        Assert.Contains(PermissionCodes.PayrollReopen, PermissionCodes.All);
    }
}
