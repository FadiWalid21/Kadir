using Kadr.Platform.Domain.Leave;

namespace Kadr.Platform.Tests.Leave;

public sealed class LeaveBalanceTests
{
    [Fact]
    public void Remaining_reflects_entitled_accrued_used_and_adjustment()
    {
        var bal = LeaveBalance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2026, 21m);
        bal.SetAccrued(2m);
        bal.SetCarriedIn(1m);
        bal.AddUsed(5m);
        bal.AddAdjustment(2m);
        Assert.Equal(21m + 2m + 1m + 2m - 5m, bal.Remaining);
    }
}
