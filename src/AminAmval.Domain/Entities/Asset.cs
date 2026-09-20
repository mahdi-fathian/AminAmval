using AminAmval.Domain.Enums;
using AminAmval.Domain.Events;
using AminAmval.Domain.Exceptions;
using AminAmval.Domain.ValueObjects;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class Asset : Entity
{
    private readonly List<Assignment> _assignments = [];
    private readonly List<DispositionRequest> _dispositionRequests = [];

    public AssetCode Code { get; private set; }
    public string OldCode { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string CategoryId { get; private set; } = "";
    public string Brand { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string Serial { get; private set; } = "";
    public AssetQuality Quality { get; private set; }
    public string Owner { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string ImageId { get; private set; } = "";
    public DateTime? PurchaseDate { get; private set; }
    public Money? PurchaseCost { get; private set; }
    public bool HasLabel { get; private set; } = true;
    public AssetStatus Status { get; private set; }
    public string Location { get; private set; } = "";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? LastOperationDate { get; private set; }
    public bool Deleted { get; private set; }

    public IReadOnlyCollection<Assignment> Assignments => _assignments.AsReadOnly();
    public IReadOnlyCollection<DispositionRequest> DispositionRequests => _dispositionRequests.AsReadOnly();

    private Asset() { } // EF Core

    public Asset(
        AssetCode code,
        string name,
        string categoryId,
        AssetQuality quality,
        string owner,
        string description,
        string imageId,
        string brand = "",
        string model = "",
        string serial = "",
        string oldCode = "",
        DateTime? purchaseDate = null,
        Money? purchaseCost = null,
        bool hasLabel = true,
        string location = "")
    {
        Code = code;
        Name = name;
        CategoryId = categoryId;
        Brand = brand;
        Model = model;
        Serial = serial;
        Quality = quality;
        Owner = owner;
        Description = description;
        ImageId = imageId;
        OldCode = oldCode;
        PurchaseDate = purchaseDate;
        PurchaseCost = purchaseCost;
        HasLabel = hasLabel;
        Location = location;
        Status = AssetStatus.Available;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name,
        string categoryId,
        AssetQuality quality,
        string owner,
        string description,
        string imageId,
        string brand,
        string model,
        string serial,
        string oldCode,
        DateTime? purchaseDate,
        Money? purchaseCost,
        bool hasLabel,
        string location)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (_dispositionRequests.Any(r => r.State == DispositionState.Pending))
            throw new AssetHasPendingDispositionRequestException(Id);

        Name = name;
        CategoryId = categoryId;
        Quality = quality;
        Owner = owner;
        Description = description;
        ImageId = imageId;
        Brand = brand;
        Model = model;
        Serial = serial;
        OldCode = oldCode;
        PurchaseDate = purchaseDate;
        PurchaseCost = purchaseCost;
        HasLabel = hasLabel;
        Location = location;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Archive(string reason, string archivedBy)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (Status == AssetStatus.Assigned)
            throw new AssetHasActiveAssignmentException(Id);

        if (_dispositionRequests.Any(r => r.State == DispositionState.Pending))
            throw new AssetHasPendingDispositionRequestException(Id);

        Deleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Restore(string reason, string restoredBy)
    {
        if (!Deleted)
            throw new InvalidOperationException("این مال بایگانی نشده است.");

        Deleted = false;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Assign(
        string userId,
        string departmentId,
        DateTime startedAt,
        string notes,
        string reference,
        string assignedBy,
        string recipientName,
        string departmentName)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (Status != AssetStatus.Available)
            throw new InvalidAssetStateTransitionException(Status, AssetStatus.Assigned, "فقط اموال موجود در انبار قابل تخصیص است.");

        var currentAssignment = _assignments.SingleOrDefault(a => a.EndedAt == null);
        if (currentAssignment != null)
            throw new InvalidOperationException("این مال در حال حاضر تخصیص دارد.");

        ValidateChronology(startedAt);

        var assignment = new Assignment(
            Id, Code.Value, Name, Serial, Quality,
            assignedBy, userId, departmentId, startedAt,
            notes, reference, recipientName, departmentName);

        _assignments.Add(assignment);
        Status = AssetStatus.Assigned;
        LastOperationDate = startedAt;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Transfer(
        string userId,
        string departmentId,
        DateTime startedAt,
        string notes,
        string reference,
        string transferredBy,
        string recipientName,
        string departmentName)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (Status != AssetStatus.Assigned)
            throw new InvalidAssetStateTransitionException(Status, AssetStatus.Assigned, "فقط اموال در حال بهره‌برداری قابل انتقال است.");

        var currentAssignment = _assignments.SingleOrDefault(a => a.EndedAt == null);
        if (currentAssignment == null)
            throw new InvalidOperationException("این مال تخصیص فعالی ندارد.");

        ValidateChronology(startedAt);

        if (startedAt < currentAssignment.StartedAt)
            throw new InvalidOperationException("تاریخ انتقال قبل از تخصیص جاری است.");

        if (currentAssignment.UserId == userId && currentAssignment.DepartmentId == departmentId)
            throw new InvalidOperationException("تحویل‌گیرندهٔ جدید با تخصیص جاری یکسان است.");

        currentAssignment.End(startedAt, "انتقال", transferredBy, reference);

        var assignment = new Assignment(
            Id, Code.Value, Name, Serial, Quality,
            transferredBy, userId, departmentId, startedAt,
            notes, reference, recipientName, departmentName);

        _assignments.Add(assignment);
        LastOperationDate = startedAt;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Return(
        DateTime returnedAt,
        string reason,
        string reference,
        string location,
        string returnedBy)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (Status != AssetStatus.Assigned)
            throw new InvalidAssetStateTransitionException(Status, AssetStatus.Available, "اموال تخصیص جاری ندارد.");

        var currentAssignment = _assignments.SingleOrDefault(a => a.EndedAt == null);
        if (currentAssignment == null)
            throw new InvalidOperationException("این مال تخصیص فعالی ندارد.");

        ValidateChronology(returnedAt);

        if (returnedAt < currentAssignment.StartedAt)
            throw new InvalidOperationException("تاریخ عودت نمی‌تواند قبل از شروع تخصیص باشد.");

        currentAssignment.End(returnedAt, reason, returnedBy, reference);

        Status = AssetStatus.Available;
        LastOperationDate = returnedAt;
        Location = location;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void ChangeStatus(
        AssetStatus newStatus,
        DateTime effectiveDate,
        string reason,
        string reference,
        Money? amount,
        string changedBy)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (_dispositionRequests.Any(r => r.State == DispositionState.Pending))
            throw new AssetHasPendingDispositionRequestException(Id);

        if (Status == AssetStatus.Assigned)
            throw new InvalidAssetStateTransitionException(Status, newStatus, "پیش از تغییر وضعیت، اموال را عودت دهید.");

        if (newStatus.IsTerminal())
            throw new InvalidAssetStateTransitionException(Status, newStatus, "برای فروش، اسقاط یا خروج ابتدا درخواست مجوز ثبت و تأیید مدیر دیگر را دریافت کنید.");

        if (!AssetStatusExtensions.AllStatuses.Contains(newStatus) || newStatus == AssetStatus.Assigned || newStatus == Status)
            throw new InvalidAssetStateTransitionException(Status, newStatus, "تغییر وضعیت مجاز نیست.");

        if (newStatus == AssetStatus.Available && Status is not (AssetStatus.Maintenance or AssetStatus.Lost))
            throw new InvalidAssetStateTransitionException(Status, newStatus, "تغییر وضعیت به موجود فقط از حالت تعمیر یا مفقود مجاز است.");

        ValidateChronology(effectiveDate);

        if (newStatus == AssetStatus.Sold && (amount is null or { Amount: <= 0 }))
            throw new InvalidOperationException("مبلغ مثبت فروش به ریال الزامی است.");

        var oldStatus = Status;
        Status = newStatus;
        LastOperationDate = effectiveDate;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void RequestDisposition(
        AssetStatus targetStatus,
        DateTime effectiveDate,
        string reason,
        string reference,
        Money? amount,
        string requestedBy,
        string requesterName)
    {
        if (Deleted)
            throw new AssetArchivedException(Id);

        if (!targetStatus.IsTerminal())
            throw new InvalidOperationException("درخواست مجوز فقط برای فروش، اسقاط یا خروج است.");

        ValidateChronology(effectiveDate);

        var request = new DispositionRequest(
            Id, targetStatus, Status, effectiveDate, reason, reference, amount,
            requestedBy, requesterName, Version);

        _dispositionRequests.Add(request);
        IncrementVersion();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApproveDisposition(string requestId, string approvedBy, string assetCode)
    {
        var request = _dispositionRequests.SingleOrDefault(r => r.Id == requestId)
            ?? throw new InvalidOperationException("درخواست پیدا نشد.");

        if (request.State != DispositionState.Pending)
            throw new InvalidDispositionStateException(request.State, "این درخواست قبلاً تعیین تکلیف شده است.");

        if (Version != request.AssetVersion)
            throw new ConcurrencyException("Asset", Id, request.AssetVersion, Version);

        request.Approve();
        Status = request.TargetStatus;
        LastOperationDate = request.EffectiveDate;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void RejectDisposition(string requestId, string rejectedBy, string note)
    {
        var request = _dispositionRequests.SingleOrDefault(r => r.Id == requestId)
            ?? throw new InvalidOperationException("درخواست پیدا نشد.");

        if (request.State != DispositionState.Pending)
            throw new InvalidDispositionStateException(request.State, "این درخواست قبلاً تعیین تکلیف شده است.");

        request.Reject(note);
        IncrementVersion();
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelDisposition(string requestId, string cancelledBy)
    {
        var request = _dispositionRequests.SingleOrDefault(r => r.Id == requestId)
            ?? throw new InvalidOperationException("درخواست پیدا نشد.");

        if (request.State != DispositionState.Pending)
            throw new InvalidDispositionStateException(request.State, "این درخواست قبلاً تعیین تکلیف شده است.");

        request.Cancel();
        IncrementVersion();
        UpdatedAt = DateTime.UtcNow;
    }

    private void ValidateChronology(DateTime date)
    {
        if (LastOperationDate.HasValue && date < LastOperationDate.Value)
            throw new InvalidOperationException("تاریخ عملیات نمی‌تواند قبل از آخرین رویداد اموال باشد.");

        if (PurchaseDate.HasValue && date.Date < PurchaseDate.Value.Date)
            throw new InvalidOperationException("تاریخ عملیات نمی‌تواند قبل از تاریخ خرید باشد.");

        var latestEnd = _assignments.Where(a => a.EndedAt != null).Max(a => a.EndedAt);
        if (latestEnd.HasValue && date < latestEnd.Value)
            throw new InvalidOperationException("تاریخ عملیات نمی‌تواند قبل از آخرین عودت یا انتقال باشد.");
    }
}