using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
