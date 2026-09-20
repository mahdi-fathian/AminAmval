using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;
    public AuditRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        => await _context.AuditEvents.AddAsync(auditEvent, cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> GetByAssetIdAsync(string assetId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await _context.AuditEvents.AsNoTracking()
            .Where(a => a.AssetId == assetId)
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> GetByUserIdAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await _context.AuditEvents.AsNoTracking()
            .Where(a => a.ActorId == userId || a.TargetUserId == userId)
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
        => await _context.AuditEvents.CountAsync(a => a.AssetId == assetId, cancellationToken);
}
