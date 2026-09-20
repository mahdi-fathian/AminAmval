using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class AuthSessionRepository : IAuthSessionRepository
{
    private readonly AppDbContext _context;
    public AuthSessionRepository(AppDbContext context) => _context = context;

    public async Task<AuthSession?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AuthSession>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await _context.Sessions.Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow).ToListAsync(cancellationToken);

    public async Task AddAsync(AuthSession session, CancellationToken cancellationToken = default)
        => await _context.Sessions.AddAsync(session, cancellationToken);

    public Task UpdateAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        _context.Sessions.Update(session);
        return Task.CompletedTask;
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _context.Sessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var s in sessions)
            s.Revoke();
    }
}
