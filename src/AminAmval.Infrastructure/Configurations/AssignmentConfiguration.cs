using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.AssetId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DepartmentId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(32);
        builder.Property(x => x.QualityAtIssue).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.AssetId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        builder.HasIndex(x => new { x.UserId, x.EndedAt });
        builder.HasIndex(x => new { x.DepartmentId, x.EndedAt });
    }
}
