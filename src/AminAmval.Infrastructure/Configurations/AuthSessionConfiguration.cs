using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.UserId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Stamp).HasMaxLength(64);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });
        builder.HasIndex(x => x.ExpiresAt);
    }
}
