namespace AminAmval.Application.DTOs;

public sealed record AuditEventDto(
    long Id,
    DateTime At,
    string? ActorId,
    string ActorName,
    string ActorRole,
    string Ip,
    string Action,
    string EntityType,
    string? EntityId,
    string? AssetId,
    string? TargetUserId,
    string Description,
    string? Before,
    string? After,
    string CorrelationId);

public sealed record AuditFilterRequest(
    string? Query,
    string? Action,
    string? EntityType,
    string? ActorId,
    string? Scope,
    string[]? EntityIds,
    DateTime? From,
    DateTime? To);

public sealed record DashboardDto(
    int PendingOperations,
    int TotalAssets,
    IReadOnlyList<AssetStatusCountDto> StatusCounts,
    IReadOnlyList<CategoryCountDto> CategoryCounts,
    int UnlabeledAssets,
    double TotalPurchaseValue,
    IReadOnlyList<RecentAssignmentDto> RecentAssignments,
    IReadOnlyList<AuditEventDto> RecentActivities,
    SetupInfoDto? Setup,
    int ActiveUsers,
    BackupStatusDto? Backup);

public sealed record AssetStatusCountDto(string Status, int Count);
public sealed record CategoryCountDto(string Id, string Name, int Count);
public sealed record RecentAssignmentDto(
    string Id,
    string AssetId,
    string AssetName,
    string AssetCode,
    string ImageId,
    string Recipient,
    string Department,
    DateTime StartedAt);

public sealed record SetupInfoDto(
    int Categories,
    int Departments,
    int Employees,
    int Assets);

public sealed record BackupStatusDto(
    bool Enabled,
    bool Running,
    string? LastError,
    DateTime? LastBackup,
    DateTime NextRun,
    int RetentionDays,
    int HourUtc,
    int MinuteUtc);

public sealed record SystemInfoDto(
    string Version,
    string Runtime,
    string Database,
    long DatabaseBytes,
    long UploadBytes,
    long AuditCount,
    int Assets,
    int Users,
    BackupStatusDto Backup,
    IReadOnlyList<BackupFileDto> Backups);

public sealed record BackupFileDto(
    string Name,
    long Size,
    DateTime CreatedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);