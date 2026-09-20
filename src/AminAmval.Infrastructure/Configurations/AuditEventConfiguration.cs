using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("Audit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.SequentialId).ValueGeneratedOnAdd();
        builder.HasIndex(x => x.At);
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.TargetUserId);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
