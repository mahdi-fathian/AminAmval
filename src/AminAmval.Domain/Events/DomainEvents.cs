using AminAmval.Domain.Enums;
using AminAmval.Domain.ValueObjects;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Events;

public sealed record AssetCreatedEvent(
    string AssetId,
    AssetCode Code,
    string Name,
    AssetStatus Status,
    string CreatedBy) : DomainEvent;

public sealed record AssetUpdatedEvent(
    string AssetId,
    AssetCode Code,
    string Name,
    string UpdatedBy) : DomainEvent;

public sealed record AssetArchivedEvent(
    string AssetId,
    AssetCode Code,
    string Reason,
    string ArchivedBy) : DomainEvent;

public sealed record AssetRestoredEvent(
    string AssetId,
    AssetCode Code,
    string Reason,
    string RestoredBy) : DomainEvent;

public sealed record AssetAssignedEvent(
    string AssetId,
    AssetCode Code,
    string AssignedToUserId,
    string AssignedToDepartmentId,
    DateTime StartedAt,
    string AssignedBy) : DomainEvent;

public sealed record AssetTransferredEvent(
    string AssetId,
    AssetCode Code,
    string FromUserId,
    string ToUserId,
    string DepartmentId,
    DateTime TransferredAt,
    string TransferredBy) : DomainEvent;

public sealed record AssetReturnedEvent(
    string AssetId,
    AssetCode Code,
    string ReturnedByUserId,
    DateTime ReturnedAt,
    string Reason,
    string ReturnedBy) : DomainEvent;

public sealed record AssetStatusChangedEvent(
    string AssetId,
    AssetCode Code,
    AssetStatus FromStatus,
    AssetStatus ToStatus,
    DateTime EffectiveDate,
    string Reason,
    string ChangedBy) : DomainEvent;

public sealed record UserCreatedEvent(
    string UserId,
    Username Username,
    PersonnelCode PersonnelCode,
    string FullName,
    UserRole Role,
    string CreatedBy) : DomainEvent;

public sealed record UserUpdatedEvent(
    string UserId,
    Username Username,
    string UpdatedBy) : DomainEvent;

public sealed record UserDeactivatedEvent(
    string UserId,
    Username Username,
    string DeactivatedBy) : DomainEvent;

public sealed record UserPasswordResetEvent(
    string UserId,
    Username Username,
    string ResetBy) : DomainEvent;

public sealed record UserPasswordChangedEvent(
    string UserId,
    Username Username) : DomainEvent;

public sealed record DispositionRequestedEvent(
    string RequestId,
    string AssetId,
    AssetCode AssetCode,
    AssetStatus TargetStatus,
    DateTime EffectiveDate,
    string RequestedBy) : DomainEvent;

public sealed record DispositionApprovedEvent(
    string RequestId,
    string AssetId,
    AssetCode AssetCode,
    AssetStatus NewStatus,
    string ApprovedBy) : DomainEvent;

public sealed record DispositionRejectedEvent(
    string RequestId,
    string AssetId,
    AssetCode AssetCode,
    string RejectedBy,
    string Note) : DomainEvent;

public sealed record DispositionCancelledEvent(
    string RequestId,
    string AssetId,
    AssetCode AssetCode,
    string CancelledBy) : DomainEvent;

public sealed record CategoryCreatedEvent(
    string CategoryId,
    string Name,
    string CreatedBy) : DomainEvent;

public sealed record CategoryUpdatedEvent(
    string CategoryId,
    string Name,
    string UpdatedBy) : DomainEvent;

public sealed record DepartmentCreatedEvent(
    string DepartmentId,
    string Name,
    string CreatedBy) : DomainEvent;

public sealed record DepartmentUpdatedEvent(
    string DepartmentId,
    string Name,
    string UpdatedBy) : DomainEvent;

public sealed record BackupCreatedEvent(
    string FileName,
    long Size,
    string CreatedBy) : DomainEvent;

public sealed record ExcelImportedEvent(
    string Kind,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    string ImportedBy) : DomainEvent;