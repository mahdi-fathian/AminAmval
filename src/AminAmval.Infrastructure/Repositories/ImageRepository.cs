using AminAmval.Domain.Entities;
using AminAmval.Domain.Repositories;
using AminAmval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Repositories;

public sealed class ImageRepository : IImageRepository
{
    private readonly AppDbContext _context;
    public ImageRepository(AppDbContext context) => _context = context;

    public async Task<UploadedImage?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Images.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _context.Images.AnyAsync(i => i.Id == id, cancellationToken);

    public async Task AddAsync(UploadedImage image, CancellationToken cancellationToken = default)
        => await _context.Images.AddAsync(image, cancellationToken);

    public async Task<IReadOnlyList<UploadedImage>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var list = ids.ToList();
        return await _context.Images.Where(i => list.Contains(i.Id)).ToListAsync(cancellationToken);
    }
}
