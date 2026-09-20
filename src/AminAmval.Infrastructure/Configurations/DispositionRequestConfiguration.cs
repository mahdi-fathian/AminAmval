using AminAmval.Domain.Entities;
using AminAmval.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class DispositionRequestConfiguration : IEntityTypeConfiguration<DispositionRequest>
{
    public void Configure(EntityTypeBuilder<DispositionRequest> builder)
    {
        builder.ToTable("DispositionRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.AssetId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TargetStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OriginalStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Amount)
            .HasConversion(
                v => v == null ? (long?)null : v.Amount,
                v => v == null ? null : new Money(v.Value));
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.AssetId).IsUnique().HasFilter("\"State\" = 'Pending'");
        builder.HasIndex(x => new { x.State, x.RequestedAt });
    }
}
