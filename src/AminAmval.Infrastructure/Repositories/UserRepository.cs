using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    public UserRepository(AppDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Username.Value == username, cancellationToken);

    public async Task<User?> GetByPersonnelCodeAsync(string personnelCode, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.PersonnelCode.Value == personnelCode, cancellationToken);

    public async Task<User?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.NationalId != null && u.NationalId.Value == nationalId, cancellationToken);

    public async Task<bool> ExistsByUsernameAsync(string username, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Users.Where(u => u.Username.Value == username);
        if (!string.IsNullOrEmpty(excludeId)) q = q.Where(u => u.Id != excludeId);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByPersonnelCodeAsync(string personnelCode, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Users.Where(u => u.PersonnelCode.Value == personnelCode);
        if (!string.IsNullOrEmpty(excludeId)) q = q.Where(u => u.Id != excludeId);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNationalIdAsync(string nationalId, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var q = _context.Users.Where(u => u.NationalId != null && u.NationalId.Value == nationalId);
        if (!string.IsNullOrEmpty(excludeId)) q = q.Where(u => u.Id != excludeId);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Users.AsNoTracking().OrderBy(u => u.LastName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
        => await _context.Users.AsNoTracking().Where(u => u.Active).OrderBy(u => u.LastName).ToListAsync(cancellationToken);
}
