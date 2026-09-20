using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class AuditEvent : Entity
{
    public long SequentialId { get; set; }
    public DateTime At { get; private set; } = DateTime.UtcNow;
    public string? ActorId { get; private set; }
    public string ActorName { get; private set; } = "";
    public string ActorRole { get; private set; } = "";
    public string Ip { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string EntityType { get; private set; } = "";
    public string? EntityId { get; private set; }
    public string? AssetId { get; private set; }
    public string? TargetUserId { get; private set; }
    public string Description { get; private set; } = "";
    public string? Before { get; private set; }
    public string? After { get; private set; }
    public string CorrelationId { get; private set; } = "";

    private AuditEvent() { } // EF Core

    public AuditEvent(
        string? actorId,
        string actorName,
        string actorRole,
        string ip,
        string action,
        string entityType,
        string? entityId,
        string description,
        object? before,
        object? after,
        string? assetId = null,
        string? targetUserId = null,
        string? correlationId = null)
    {
        ActorId = actorId;
        ActorName = actorName;
        ActorRole = actorRole;
        Ip = ip;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Description = description;
        AssetId = assetId ?? (entityType == "Asset" ? entityId : null);
        TargetUserId = targetUserId ?? (entityType == "User" ? entityId : null);
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
        At = DateTime.UtcNow;

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        Before = before == null ? null : System.Text.Json.JsonSerializer.Serialize(before, options);
        After = after == null ? null : System.Text.Json.JsonSerializer.Serialize(after, options);
    }
}