using System.Text.Json.Serialization;
using AminAmval.Domain.Enums;

namespace AminAmval.Application.DTOs;

public sealed record AssetDto(
    string Id,
    string Code,
    string OldCode,
    string Name,
    string CategoryId,
    string CategoryName,
    string Brand,
    string Model,
    string Serial,
    string Quality,
    string Owner,
    string Description,
    string ImageId,
    DateTime? PurchaseDate,
    long? PurchaseCost,
    bool HasLabel,
    string Status,
    string Location,
    bool Deleted,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AssignmentDto? CurrentAssignment,
    int? PendingDispositionRequestId);

public sealed record AssignmentDto(
    string Id,
    string AssetId,
    string? UserId,
    string DepartmentId,
    string UserName,
    string DepartmentName,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Notes,
    string Reference,
    string EndReason);

public sealed record AssetListItemDto(
    string Id,
    string Code,
    string OldCode,
    string Name,
    string CategoryId,
    string CategoryName,
    string Brand,
    string Model,
    string Serial,
    string Quality,
    string Owner,
    string Status,
    bool HasLabel,
    DateTime? PurchaseDate,
    long? PurchaseCost,
    string Location,
    int Version,
    DateTime CreatedAt,
    string? CurrentAssigneeName,
    string? CurrentDepartmentName);

public sealed record CreateAssetRequest(
    string Code,
    string OldCode,
    string Name,
    string CategoryId,
    string Brand,
    string Model,
    string Serial,
    string Quality,
    string Owner,
    string Description,
    string ImageId,
    DateTime? PurchaseDate,
    long? PurchaseCost,
    bool HasLabel,
    string Location);

public sealed record UpdateAssetRequest(
    string Code,
    string OldCode,
    string Name,
    string CategoryId,
    string Brand,
    string Model,
    string Serial,
    string Quality,
    string Owner,
    string Description,
    string ImageId,
    DateTime? PurchaseDate,
    long? PurchaseCost,
    bool HasLabel,
    string Location,
    int Version);

public sealed record AssignAssetRequest(
    string? UserId,
    string? DepartmentId,
    DateTime StartedAt,
    string? Notes,
    string? Reference,
    int Version);

public sealed record TransferAssetRequest(
    string? UserId,
    string? DepartmentId,
    DateTime StartedAt,
    string? Notes,
    string? Reference,
    int Version);

public sealed record ReturnAssetRequest(
    DateTime Date,
    string Reason,
    string? Reference,
    string? Location,
    int Version);

public sealed record ChangeAssetStatusRequest(
    string Status,
    DateTime Date,
    string Reason,
    string Reference,
    long? Amount,
    int Version);

public sealed record DispositionRequestRequest(
    string Status,
    DateTime Date,
    string Reason,
    string Reference,
    long? Amount,
    int Version);

public sealed record DispositionDecisionRequest(
    string Note,
    int Version);

public sealed record ArchiveAssetRequest(
    string Reason,
    int Version);

public sealed record RestoreAssetRequest(
    string Reason,
    int Version);