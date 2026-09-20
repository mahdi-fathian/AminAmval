using AminAmval.Domain.Enums;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class Assignment : Entity
{
    public string AssetId { get; private set; } = "";
    public string AssetCodeAtIssue { get; private set; } = "";
    public string AssetNameAtIssue { get; private set; } = "";
    public string SerialAtIssue { get; private set; } = "";
    public AssetQuality QualityAtIssue { get; private set; }
    public string IssuerName { get; private set; } = "";
    public string? UserId { get; private set; }
    public string DepartmentId { get; private set; } = "";
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string Notes { get; private set; } = "";
    public string Reference { get; private set; } = "";
    public string EndReason { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public string RecipientName { get; private set; } = "";
    public string DepartmentName { get; private set; } = "";
    public string EndedBy { get; private set; } = "";
    public string EndReference { get; private set; } = "";

    private Assignment() { } // EF Core

    public Assignment(
        string assetId,
        string assetCodeAtIssue,
        string assetNameAtIssue,
        string serialAtIssue,
        AssetQuality qualityAtIssue,
        string issuerName,
        string? userId,
        string departmentId,
        DateTime startedAt,
        string notes,
        string reference,
        string recipientName,
        string departmentName)
    {
        AssetId = assetId;
        AssetCodeAtIssue = assetCodeAtIssue;
        AssetNameAtIssue = assetNameAtIssue;
        SerialAtIssue = serialAtIssue;
        QualityAtIssue = qualityAtIssue;
        IssuerName = issuerName;
        UserId = userId;
        DepartmentId = departmentId;
        StartedAt = startedAt;
        Notes = notes;
        Reference = reference;
        CreatedBy = CreatedBy;
        RecipientName = recipientName;
        DepartmentName = departmentName;
    }

    public void End(DateTime endedAt, string endReason, string endedBy, string endReference)
    {
        if (EndedAt != null)
            throw new InvalidOperationException("این تخصیص قبلاً خاتمه یافته است.");

        EndedAt = endedAt;
        EndReason = endReason;
        EndedBy = endedBy;
        EndReference = endReference;
        IncrementVersion();
    }

    public bool IsActive => EndedAt == null;
}