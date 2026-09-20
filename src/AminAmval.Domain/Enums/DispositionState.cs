namespace AminAmval.Domain.Enums;

public enum DispositionState
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public static class DispositionStateExtensions
{
    public static string ToPersian(this DispositionState state) => state switch
    {
        DispositionState.Pending => "در انتظار",
        DispositionState.Approved => "تأیید شده",
        DispositionState.Rejected => "رد شده",
        DispositionState.Cancelled => "لغو شده",
        _ => state.ToString()
    };

    public static readonly DispositionState[] AllStates = Enum.GetValues<DispositionState>();

    public static DispositionState FromString(string state) => state switch
    {
        "Pending" => DispositionState.Pending,
        "Approved" => DispositionState.Approved,
        "Rejected" => DispositionState.Rejected,
        "Cancelled" => DispositionState.Cancelled,
        _ => throw new ArgumentException($"وضعیت درخواست نامعتبر: {state}")
    };
}