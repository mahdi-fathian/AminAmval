using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly AppDbContext _context;
    public AssignmentRepository(AppDbContext context) => _context = context;

    public async Task<Assignment?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Assignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Assignment?> GetActiveByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
        => await _context.Assignments.FirstOrDefaultAsync(a => a.AssetId == assetId && a.EndedAt == null, cancellationToken);

    public async Task<IReadOnlyList<Assignment>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default)
        => await _context.Assignments.AsNoTracking().Where(a => a.AssetId == assetId).OrderByDescending(a => a.StartedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Assignment>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await _context.Assignments.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.StartedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(Assignment assignment, CancellationToken cancellationToken = default)
        => await _context.Assignments.AddAsync(assignment, cancellationToken);

    public Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        _context.Assignments.Update(assignment);
        return Task.CompletedTask;
    }
}
