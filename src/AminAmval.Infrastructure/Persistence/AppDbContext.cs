using AminAmval.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<UploadedImage> Images => Set<UploadedImage>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<DispositionRequest> DispositionRequests => Set<DispositionRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        base.OnModelCreating(modelBuilder);
    }
}
