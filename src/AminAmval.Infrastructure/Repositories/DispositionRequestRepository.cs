using AminAmval.Domain.Entities;
using AminAmval.Domain.Enums;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class DispositionRequestRepository : IDispositionRequestRepository
{
    private readonly AppDbContext _context;
    public DispositionRequestRepository(AppDbContext context) => _context = context;

    public async Task<DispositionRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.DispositionRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<DispositionRequest?> GetPendingByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
        => await _context.DispositionRequests.FirstOrDefaultAsync(r => r.AssetId == assetId && r.State == DispositionState.Pending, cancellationToken);

    public async Task<IReadOnlyList<DispositionRequest>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
        => await _context.DispositionRequests.AsNoTracking().Where(r => r.AssetId == assetId).OrderByDescending(r => r.RequestedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(DispositionRequest request, CancellationToken cancellationToken = default)
        => await _context.DispositionRequests.AddAsync(request, cancellationToken);

    public Task UpdateAsync(DispositionRequest request, CancellationToken cancellationToken = default)
    {
        _context.DispositionRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DispositionRequest>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await _context.DispositionRequests.AsNoTracking().Where(r => r.State == DispositionState.Pending).OrderBy(r => r.RequestedAt).ToListAsync(cancellationToken);
}
