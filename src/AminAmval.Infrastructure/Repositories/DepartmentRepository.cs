using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _context;
    public DepartmentRepository(AppDbContext context) => _context = context;

    public async Task<Department?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<Department?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.Departments.FirstOrDefaultAsync(d => d.Name == name, cancellationToken);

    public async Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Departments.Where(d => d.Name == name);
        if (!string.IsNullOrEmpty(excludeId)) q = q.Where(d => d.Id != excludeId);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Department department, CancellationToken cancellationToken = default)
        => await _context.Departments.AddAsync(department, cancellationToken);

    public Task UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        _context.Departments.Update(department);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Department department, CancellationToken cancellationToken = default)
    {
        _context.Departments.Remove(department);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
}
