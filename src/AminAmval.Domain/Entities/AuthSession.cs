using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class AuthSession : Entity
{
    public string UserId { get; private set; } = "";
    public string Stamp { get; private set; } = "";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; private set; } = DateTime.UtcNow.AddHours(8);
    public DateTime? RevokedAt { get; private set; }
    public string Ip { get; private set; } = "";
    public string UserAgent { get; private set; } = "";

    private AuthSession() { } // EF Core

    public AuthSession(string userId, string stamp, string ip, string userAgent)
    {
        UserId = userId;
        Stamp = stamp;
        Ip = ip;
        UserAgent = userAgent;
        CreatedAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddHours(8);
    }

    public void UpdateLastSeen()
    {
        LastSeenAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddHours(8);
        IncrementVersion();
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
}