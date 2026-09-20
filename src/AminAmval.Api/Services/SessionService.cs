using AminAmval.Data;
using AminAmval.Models;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Services;
public static class SessionService
{
    public static AuthSession Create(AppDb db, AppUser user, HttpContext c)
    {
        var agent = c.Request.Headers.UserAgent.ToString();
        var s = new AuthSession { UserId = user.Id, Stamp = user.SecurityStamp, Ip = c.Connection.RemoteIpAddress?.ToString() ?? "", UserAgent = agent[..Math.Min(300, agent.Length)] };
        db.Sessions.Add(s); return s;
    }
    // Tracked changes are committed together with the profile/password and its audit.
    public static async Task RevokeAll(AppDb db, string userId)
    {
        foreach (var s in await db.Sessions.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync()) s.RevokedAt = DateTime.UtcNow;
    }
}
