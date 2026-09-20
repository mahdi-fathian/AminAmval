using System.Text.RegularExpressions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.ValueObjects;

public sealed class AssetCode : ValueObject
{
    private static readonly Regex Pattern = new("^[A-Z0-9\\-]{3,60}$", RegexOptions.Compiled);

    public string Value { get; }

    public AssetCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("کد اموال نمی‌تواند خالی باشد.", nameof(value));

        var cleaned = value.Trim().ToUpperInvariant();
        if (!Pattern.IsMatch(cleaned))
            throw new ArgumentException("کد اموال فقط می‌تواند شامل حروف لاتین، ارقام و خط تیره باشد.", nameof(value));

        Value = cleaned;
    }

    public static AssetCode Generate() =>
        new($"NV-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(AssetCode code) => code.Value;
    public static explicit operator AssetCode(string value) => new(value);
}
