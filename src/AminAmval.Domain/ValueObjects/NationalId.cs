using System.Text.RegularExpressions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.ValueObjects;

public sealed class NationalId : ValueObject
{
    private static readonly Regex Pattern = new("^[0-9]{10}$", RegexOptions.Compiled);

    public string Value { get; }

    public NationalId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("کد ملی نمی‌تواند خالی باشد.", nameof(value));

        var digits = value.Trim();
        if (!Pattern.IsMatch(digits))
            throw new ArgumentException("کد ملی باید ۱۰ رقم عددی باشد.", nameof(value));

        if (digits.Distinct().Count() == 1)
            throw new ArgumentException("کد ملی نامعتبر است.", nameof(value));

        var sum = digits.Take(9).Select((c, i) => (c - '0') * (10 - i)).Sum() % 11;
        var checkDigit = sum < 2 ? sum : 11 - sum;
        if (digits[9] - '0' != checkDigit)
            throw new ArgumentException("کد ملی نامعتبر است.", nameof(value));

        Value = digits;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(NationalId id) => id.Value;
    public static explicit operator NationalId(string value) => new(value);
}
