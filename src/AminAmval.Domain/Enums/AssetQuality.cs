namespace AminAmval.Domain.Enums;

public enum AssetQuality
{
    New = 1,
    Good = 2,
    Used = 3,
    Damaged = 4
}

public static class AssetQualityExtensions
{
    public static string ToPersian(this AssetQuality quality) => quality switch
    {
        AssetQuality.New => "نو",
        AssetQuality.Good => "سالم",
        AssetQuality.Used => "کارکرده",
        AssetQuality.Damaged => "معیوب",
        _ => quality.ToString()
    };

    public static AssetQuality FromString(string quality) => quality switch
    {
        "New" or "نو" => AssetQuality.New,
        "Good" or "سالم" => AssetQuality.Good,
        "Used" or "کارکرده" => AssetQuality.Used,
        "Damaged" or "معیوب" => AssetQuality.Damaged,
        _ => throw new ArgumentException($"کیفیت نامعتبر: {quality}")
    };
}