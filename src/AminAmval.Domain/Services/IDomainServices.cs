using AminAmval.Domain.Entities;
using AminAmval.Domain.ValueObjects;

namespace AminAmval.Domain.Services;

public interface IImageStorageService
{
    Task<UploadedImage> StoreAsync(byte[] imageBytes, string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string imageId, CancellationToken cancellationToken = default);
    Task<byte[]> GetAsync(string imageId, CancellationToken cancellationToken = default);
    (string Extension, string ContentType) ValidateImage(byte[] imageBytes);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
}

public interface IUniqueCodeGenerator
{
    AssetCode GenerateAssetCode();
}
