using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AminAmval.Models;

public static class Roles
{
    public const string Admin = "Admin", Custodian = "Custodian", Employee = "Employee";
    public static readonly string[] All = [Admin, Custodian, Employee];
}
public static class States
{
    public const string Available = "Available", Assigned = "Assigned", Maintenance = "Maintenance", Scrapped = "Scrapped", Sold = "Sold", Lost = "Lost", Exited = "Exited";
    public static readonly string[] All = [Available, Assigned, Maintenance, Scrapped, Sold, Lost, Exited];
    public static string Fa(string s) => s switch { Available => "موجود در انبار", Assigned => "در حال بهره‌برداری", Maintenance => "در تعمیر", Scrapped => "اسقاط‌شده", Sold => "فروخته‌شده", Lost => "مفقودشده", Exited => "خارج‌شده", _ => s };
    public static readonly string[] Terminal = [Scrapped, Sold, Exited];
}
public class Department
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = "";
    public string PersonnelCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? NationalId { get; set; }
    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Role { get; set; } = Roles.Employee;
    public bool Active { get; set; } = true;
    [JsonIgnore] public string PasswordHash { get; set; } = "";
    [JsonIgnore] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool MustChangePassword { get; set; } = true;
    [JsonIgnore] public int FailedLogins { get; set; }
    [JsonIgnore] public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public class Asset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Code { get; set; } = "";
    public string OldCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public Category Category { get; set; } = null!;
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Quality { get; set; } = "Good";
    public string Owner { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageId { get; set; } = "";
    public DateTime? PurchaseDate { get; set; }
    public long? PurchaseCost { get; set; }
    public bool HasLabel { get; set; } = true;
    public string Status { get; set; } = States.Available;
    public string Location { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastOperationDate { get; set; }
    public bool Deleted { get; set; }
    [ConcurrencyCheck] public int Version { get; set; } = 1;
    public List<Assignment> Assignments { get; set; } = [];
    public List<DispositionRequest> DispositionRequests { get; set; } = [];
}
public class Assignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssetId { get; set; } = "";
    [JsonIgnore] public Asset Asset { get; set; } = null!;
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public string DepartmentId { get; set; } = "";
    public Department Department { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Notes { get; set; } = "";
    public string Reference { get; set; } = "";
    public string EndReason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string AssetCodeAtIssue { get; set; } = "";
    public string AssetNameAtIssue { get; set; } = "";
    public string SerialAtIssue { get; set; } = "";
    public string QualityAtIssue { get; set; } = "";
    public string IssuerName { get; set; } = "";
    public string EndedBy { get; set; } = "";
    public string EndReference { get; set; } = "";
}
public class AuditEvent
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? AssetId { get; set; }
    public string? TargetUserId { get; set; }
    public string Description { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
    public string CorrelationId { get; set; } = "";
}
public class UploadedImage
{
    public string Id { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long Size { get; set; }
}
public record LoginInput(string Username, string Password);
public record PasswordInput(string CurrentPassword, string NewPassword);
public record ResetPasswordInput(string NewPassword);
public record ReferenceInput(string Name, string? Description, int Version = 0);
public record DeleteInput(string Reason, int Version);
public class UserInput
{
    public string PersonnelCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string NationalId { get; set; } = "";
    public string DepartmentId { get; set; } = "";
    public string Role { get; set; } = Roles.Employee;
    public string? Password { get; set; }
    public bool Active { get; set; } = true;
    public int Version { get; set; }
}
public class AssetInput
{
    public string Code { get; set; } = "";
    public string OldCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Quality { get; set; } = "Good";
    public string Owner { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageId { get; set; } = "";
    public DateTime? PurchaseDate { get; set; }
    public long? PurchaseCost { get; set; }
    public bool HasLabel { get; set; } = true;
    public string Location { get; set; } = "";
    public int Version { get; set; }
}
public record AssignmentInput(string? UserId, string? DepartmentId, DateTime StartedAt, string? Notes, string? Reference, int Version);
public record ReturnInput(DateTime Date, string Reason, string? Reference, string? Location, int Version);
public record StatusInput(string Status, DateTime Date, string Reason, string Reference, long? Amount, int Version);
public record CancelInput(string Operation, string? EntityId);

public class AuthSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    [JsonIgnore] public AppUser User { get; set; } = null!;
    [JsonIgnore] public string Stamp { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(8);
    public DateTime? RevokedAt { get; set; }
    public string Ip { get; set; } = "";
    public string UserAgent { get; set; } = "";
}
public class DispositionRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AssetId { get; set; } = "";
    [JsonIgnore] public Asset Asset { get; set; } = null!;
    public string TargetStatus { get; set; } = "";
    public string OriginalStatus { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public long? Amount { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string RequestedBy { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public string? DeciderName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string DecisionNote { get; set; } = "";
    public int AssetVersion { get; set; }
    [ConcurrencyCheck] public int Version { get; set; } = 1;
}
public record DecisionInput(string Note, int Version);
