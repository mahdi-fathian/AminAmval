using System.Text.RegularExpressions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.ValueObjects;

public sealed class Username : ValueObject
{
    private static readonly Regex Pattern = new("^[a-z0-9][a-z0-9._-]{2,59}$", RegexOptions.Compiled);

    public string Value { get; }

    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("نام کاربری نمی‌تواند خالی باشد.", nameof(value));

        var cleaned = value.Trim().ToLowerInvariant();
        if (!Pattern.IsMatch(cleaned))
            throw new ArgumentException("نام کاربری باید ۳ تا ۶۰ کاراکتر باشد و فقط شامل حروف لاتین کوچک، ارقام، نقطه، خط تیره و زیرخط می‌باشد.", nameof(value));

        Value = cleaned;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(Username u) => u.Value;
    public static explicit operator Username(string value) => new(value);
}
