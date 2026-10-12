namespace Kadr.Platform.Domain.Payroll;

public enum PayrollRunStatus
{
    Draft = 0,
    Calculating = 1,
    InReview = 2,
    Approved = 3,
    Locked = 4,
    Superseded = 5,
}
