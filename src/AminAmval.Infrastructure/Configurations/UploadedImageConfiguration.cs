using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class UploadedImageConfiguration : IEntityTypeConfiguration<UploadedImage>
{
    public void Configure(EntityTypeBuilder<UploadedImage> builder)
    {
        builder.ToTable("Images");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.OwnerUserId).HasMaxLength(32);
    }
}
