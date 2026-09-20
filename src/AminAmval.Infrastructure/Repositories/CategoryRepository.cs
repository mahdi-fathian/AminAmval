using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;
    public CategoryRepository(AppDbContext context) => _context = context;

    public async Task<Category?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.Categories.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

    public async Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Categories.Where(c => c.Name == name);
        if (!string.IsNullOrEmpty(excludeId)) q = q.Where(c => c.Id != excludeId);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        => await _context.Categories.AddAsync(category, cancellationToken);

    public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        _context.Categories.Update(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        _context.Categories.Remove(category);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
}
