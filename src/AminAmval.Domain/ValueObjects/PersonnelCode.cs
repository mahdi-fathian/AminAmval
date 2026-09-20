using System.Text.RegularExpressions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.ValueObjects;

public sealed class PersonnelCode : ValueObject
{
    private static readonly Regex Pattern = new("^[A-Z0-9\\-]{3,60}$", RegexOptions.Compiled);

    public string Value { get; }

    public PersonnelCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("کد پرسنلی نمی‌تواند خالی باشد.", nameof(value));

        var cleaned = value.Trim().ToUpperInvariant();
        if (!Pattern.IsMatch(cleaned))
            throw new ArgumentException("کد پرسنلی فقط می‌تواند شامل حروف لاتین، ارقام و خط تیره باشد.", nameof(value));

        Value = cleaned;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(PersonnelCode code) => code.Value;
    public static explicit operator PersonnelCode(string value) => new(value);
}
