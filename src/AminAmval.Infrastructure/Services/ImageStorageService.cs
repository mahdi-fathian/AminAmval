using AminAmval.Domain.Entities;
using AminAmval.Domain.Services;

namespace AminAmval.Infrastructure.Services;

public sealed class ImageStorageService : IImageStorageService
{
    private readonly string _root;

    public ImageStorageService(Microsoft.Extensions.Configuration.IConfiguration configuration,
        Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(_root);
    }

    public (string Extension, string ContentType) ValidateImage(byte[] imageBytes)
    {
        if (imageBytes.Length < 4) throw new ArgumentException("فایل تصویر نامعتبر است.");
        // JPEG
        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) return (".jpg", "image/jpeg");
        // PNG
        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50) return (".png", "image/png");
        throw new ArgumentException("فقط تصویر JPEG یا PNG مجاز است.");
    }

    public async Task<UploadedImage> StoreAsync(byte[] imageBytes, string userId, CancellationToken cancellationToken = default)
    {
        var (ext, contentType) = ValidateImage(imageBytes);
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(_root, id + ext);
        await File.WriteAllBytesAsync(path, imageBytes, cancellationToken);
        return new UploadedImage(id, contentType, userId, imageBytes.Length);
    }

    public Task DeleteAsync(string imageId, CancellationToken cancellationToken = default)
    {
        foreach (var file in Directory.GetFiles(_root, imageId + ".*"))
            File.Delete(file);
        return Task.CompletedTask;
    }

    public async Task<byte[]> GetAsync(string imageId, CancellationToken cancellationToken = default)
    {
        var file = Directory.GetFiles(_root, imageId + ".*").FirstOrDefault()
            ?? throw new FileNotFoundException("تصویر یافت نشد.", imageId);
        return await File.ReadAllBytesAsync(file, cancellationToken);
    }
}
