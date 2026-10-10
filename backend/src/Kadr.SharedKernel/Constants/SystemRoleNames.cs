namespace Kadr.SharedKernel.Constants;

public static class SystemRoleNames
{
    public const string Owner = "Owner";
    public const string HrAdmin = "HR Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Accountant = "Accountant";

    public static readonly IReadOnlyList<string> All =
    [
        Owner,
        HrAdmin,
        Manager,
        Employee,
        Accountant,
    ];
}
