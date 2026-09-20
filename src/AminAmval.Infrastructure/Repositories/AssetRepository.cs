using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly AppDbContext _context;

    public AssetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Asset?> GetByIdAsync(string id, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Assets.AsQueryable();
        if (!includeArchived)
            query = query.Where(a => !a.Deleted);
        return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Asset?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _context.Assets.FirstOrDefaultAsync(a => a.Code.Value == code && !a.Deleted, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(string code, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Assets.Where(a => a.Code.Value == code && !a.Deleted);
        if (!string.IsNullOrEmpty(excludeId))
            query = query.Where(a => a.Id != excludeId);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
        => await _context.Assets.AddAsync(asset, cancellationToken);

    public Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        _context.Assets.Update(asset);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Asset>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Assets.AsNoTracking();
        if (!includeArchived)
            query = query.Where(a => !a.Deleted);
        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Assets.Where(a => idList.Contains(a.Id)).ToListAsync(cancellationToken);
    }
}
