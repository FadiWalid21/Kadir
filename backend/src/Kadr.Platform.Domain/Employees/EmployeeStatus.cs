namespace Kadr.Platform.Domain.Employees;

public enum EmployeeStatus
{
    Active = 0,
    Probation = 1,
    OnLeave = 2,
    Suspended = 3,
    Terminated = 4,
}

public enum EmployeeGender
{
    Unspecified = 0,
    Male = 1,
    Female = 2,
}

public enum ContractType
{
    Unlimited = 0,
    FixedTerm = 1,
    Other = 2,
}

public static class EmployeeStatusRules
{
    public static bool CountsTowardCapacity(EmployeeStatus status) =>
        status is EmployeeStatus.Active or EmployeeStatus.Probation or EmployeeStatus.OnLeave;
}
