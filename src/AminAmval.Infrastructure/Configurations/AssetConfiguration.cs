using AminAmval.Domain.Entities;
using AminAmval.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);

        builder.Property(x => x.Code)
            .HasConversion(v => v.Value, v => new AssetCode(v))
            .HasColumnName("Code")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.OldCode).HasMaxLength(60);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CategoryId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.Serial).HasMaxLength(100);
        builder.Property(x => x.Owner).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ImageId).HasMaxLength(32);
        builder.Property(x => x.Location).HasMaxLength(200);

        builder.Property(x => x.PurchaseCost)
            .HasConversion(
                v => v == null ? (long?)null : v.Amount,
                v => v == null ? null : new Money(v.Value))
            .HasColumnName("PurchaseCost");

        builder.Property(x => x.Quality).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(x => new { x.Deleted, x.Status });
        builder.HasIndex(x => x.Serial);
        builder.HasIndex(x => x.PurchaseDate);

        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasMany(x => x.Assignments).WithOne().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.DispositionRequests).WithOne().HasForeignKey(d => d.AssetId).OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.DispositionRequests).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
