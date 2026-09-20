using AminAmval.Models;
using Microsoft.EntityFrameworkCore;

namespace AminAmval.Data;
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<UploadedImage> Images => Set<UploadedImage>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<DispositionRequest> DispositionRequests => Set<DispositionRequest>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.PersonnelCode).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.NationalId).IsUnique();
        b.Entity<Asset>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Department>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Asset>().HasIndex(x => new { x.Deleted, x.Status });
        b.Entity<Asset>().HasIndex(x => x.Serial);
        b.Entity<Asset>().HasIndex(x => x.PurchaseDate);
        b.Entity<Assignment>().HasIndex(x => x.AssetId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        b.Entity<Assignment>().HasIndex(x => new { x.UserId, x.EndedAt });
        b.Entity<Assignment>().HasIndex(x => new { x.DepartmentId, x.EndedAt });
        b.Entity<AuditEvent>().HasIndex(x => x.At);
        b.Entity<AuditEvent>().HasIndex(x => x.AssetId);
        b.Entity<AuditEvent>().HasIndex(x => x.TargetUserId);
        b.Entity<AuditEvent>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<AuthSession>().HasIndex(x => new { x.UserId, x.RevokedAt });
        b.Entity<AuthSession>().HasIndex(x => x.ExpiresAt);
        b.Entity<DispositionRequest>().HasIndex(x => x.AssetId).IsUnique().HasFilter("\"State\" = 'Pending'");
        b.Entity<DispositionRequest>().HasIndex(x => new { x.State, x.RequestedAt });
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys())) fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
