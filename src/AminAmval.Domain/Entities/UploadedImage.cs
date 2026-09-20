using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class UploadedImage : Entity
{
    public string ContentType { get; private set; } = "";
    public string OwnerUserId { get; private set; } = "";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public long Size { get; private set; }

    private UploadedImage() { } // EF Core

    public UploadedImage(string id, string contentType, string ownerUserId, long size)
    {
        Id = id;
        ContentType = contentType;
        OwnerUserId = ownerUserId;
        Size = size;
        CreatedAt = DateTime.UtcNow;
    }
}