using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AminAmval.Infrastructure.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
