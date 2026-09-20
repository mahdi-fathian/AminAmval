using AminAmval.Domain.Enums;
using AminAmval.Domain.ValueObjects;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class DispositionRequest : Entity
{
    public string AssetId { get; private set; } = "";
    public AssetStatus TargetStatus { get; private set; }
    public AssetStatus OriginalStatus { get; private set; }
    public DispositionState State { get; private set; } = DispositionState.Pending;
    public string Reason { get; private set; } = "";
    public string Reference { get; private set; } = "";
    public Money? Amount { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public string RequestedBy { get; private set; } = "";
    public string RequesterName { get; private set; } = "";
    public DateTime RequestedAt { get; private set; } = DateTime.UtcNow;
    public string? DecidedBy { get; private set; }
    public string? DeciderName { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string DecisionNote { get; private set; } = "";
    public int AssetVersion { get; private set; }

    private DispositionRequest() { } // EF Core

    public DispositionRequest(
        string assetId,
        AssetStatus targetStatus,
        AssetStatus originalStatus,
        DateTime effectiveDate,
        string reason,
        string reference,
        Money? amount,
        string requestedBy,
        string requesterName,
        int assetVersion)
    {
        AssetId = assetId;
        TargetStatus = targetStatus;
        OriginalStatus = originalStatus;
        EffectiveDate = effectiveDate;
        Reason = reason;
        Reference = reference;
        Amount = amount;
        RequestedBy = requestedBy;
        RequesterName = requesterName;
        AssetVersion = assetVersion;
        RequestedAt = DateTime.UtcNow;
        State = DispositionState.Pending;
    }

    public void Approve()
    {
        if (State != DispositionState.Pending)
            throw new InvalidOperationException("این درخواست قبلاً تعیین تکلیف شده است.");

        State = DispositionState.Approved;
        DecidedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Reject(string note)
    {
        if (State != DispositionState.Pending)
            throw new InvalidOperationException("این درخواست قبلاً تعیین تکلیف شده است.");

        State = DispositionState.Rejected;
        DecisionNote = note;
        DecidedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Cancel()
    {
        if (State != DispositionState.Pending)
            throw new InvalidOperationException("این درخواست قبلاً تعیین تکلیف شده است.");

        State = DispositionState.Cancelled;
        DecidedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetDecisionInfo(string decidedBy, string deciderName)
    {
        DecidedBy = decidedBy;
        DeciderName = deciderName;
    }
}