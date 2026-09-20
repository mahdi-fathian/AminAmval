using AminAmval.Domain.Exceptions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public static readonly Money Zero = new(0);
    public const long MaxAmount = 1_000_000_000_000_000L;

    public long Amount { get; }

    public Money(long amount)
    {
        if (amount < 0)
            throw new InvalidMoneyAmountException(amount, "مبلغ نمی‌تواند منفی باشد.");
        if (amount > MaxAmount)
            throw new InvalidMoneyAmountException(amount, $"مبلغ نمی‌تواند بیشتر از {MaxAmount:N0} ریال باشد.");

        Amount = amount;
    }

    public Money Add(Money other) => new(Amount + other.Amount);
    public Money Subtract(Money other) => new(Amount - other.Amount);
    public Money Multiply(decimal factor) => new((long)Math.Round(Amount * factor));

    public static Money FromRials(long rials) => new(rials);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
    }

    public override string ToString() => $"{Amount:N0} ریال";

    public static implicit operator long(Money money) => money.Amount;
    public static explicit operator Money(long amount) => new(amount);
}
