namespace AminAmval.Domain.Enums;

public enum AssetStatus
{
    Available = 1,
    Assigned = 2,
    Maintenance = 3,
    Scrapped = 4,
    Sold = 5,
    Lost = 6,
    Exited = 7
}

public static class AssetStatusExtensions
{
    public static string ToPersian(this AssetStatus status) => status switch
    {
        AssetStatus.Available => "موجود در انبار",
        AssetStatus.Assigned => "در حال بهره‌برداری",
        AssetStatus.Maintenance => "در تعمیر",
        AssetStatus.Scrapped => "اسقاط‌شده",
        AssetStatus.Sold => "فروخته‌شده",
        AssetStatus.Lost => "مفقودشده",
        AssetStatus.Exited => "خارج‌شده",
        _ => status.ToString()
    };

    public static readonly AssetStatus[] TerminalStatuses = [AssetStatus.Scrapped, AssetStatus.Sold, AssetStatus.Exited];
    public static readonly AssetStatus[] AllStatuses = Enum.GetValues<AssetStatus>();

    public static bool IsTerminal(this AssetStatus status) => TerminalStatuses.Contains(status);

    public static AssetStatus FromString(string status) => status switch
    {
        "Available" => AssetStatus.Available,
        "Assigned" => AssetStatus.Assigned,
        "Maintenance" => AssetStatus.Maintenance,
        "Scrapped" => AssetStatus.Scrapped,
        "Sold" => AssetStatus.Sold,
        "Lost" => AssetStatus.Lost,
        "Exited" => AssetStatus.Exited,
        _ => throw new ArgumentException($"وضعیت نامعتبر: {status}")
    };
}