namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>Money helpers for payroll. Scale is decimal(18,2); never use double/float.</summary>
public static class PayrollMoney
{
    public const int Scale = 2;

    /// <summary>
    /// Rounds per <paramref name="policy"/>. Supported: half-away-from-zero to 2 decimals.
    /// </summary>
    public static decimal Round(decimal amount, RoundingPolicy policy)
    {
        return policy.Mode switch
        {
            RoundingMode.HalfAwayFromZero => Math.Round(amount, policy.DecimalPlaces, MidpointRounding.AwayFromZero),
            _ => Math.Round(amount, policy.DecimalPlaces, MidpointRounding.AwayFromZero),
        };
    }
}

public enum RoundingMode
{
    HalfAwayFromZero = 0,
}

public readonly record struct RoundingPolicy(RoundingMode Mode, int DecimalPlaces)
{
    public static RoundingPolicy HalfAwayFromZero2 { get; } = new(RoundingMode.HalfAwayFromZero, PayrollMoney.Scale);
}
