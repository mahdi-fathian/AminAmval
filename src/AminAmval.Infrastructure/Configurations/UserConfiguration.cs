using AminAmval.Domain.Entities;
using AminAmval.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);

        builder.Property(x => x.Username)
            .HasConversion(v => v.Value, v => new Username(v))
            .HasMaxLength(60)
            .IsRequired();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.Property(x => x.PersonnelCode)
            .HasConversion(v => v.Value, v => new PersonnelCode(v))
            .HasMaxLength(60)
            .IsRequired();
        builder.HasIndex(x => x.PersonnelCode).IsUnique();

        builder.Property(x => x.NationalId)
            .HasConversion(
                v => v == null ? null : v.Value,
                v => string.IsNullOrEmpty(v) ? null : new NationalId(v))
            .HasMaxLength(10);
        builder.HasIndex(x => x.NationalId).IsUnique();

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DepartmentId).HasMaxLength(32);
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.SecurityStamp).HasMaxLength(64);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
