namespace FinGuard.Compliance.Domain;

/// <summary>A non-negative amount in USD (ADR-0010).</summary>
public readonly record struct Money
{
    public Money(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Amount = amount;
    }

    public decimal Amount { get; }

    // D-162: no amounts in ToString, which ends up in logs and messages.
    public override string ToString() => nameof(Money);
}
