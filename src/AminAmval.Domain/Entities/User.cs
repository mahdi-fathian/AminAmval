using AminAmval.Domain.Enums;
using AminAmval.Domain.ValueObjects;
using AminAmval.Domain.Events;
using AminAmval.Domain.Exceptions;
using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class User : Entity
{
    public Username Username { get; private set; }
    public PersonnelCode PersonnelCode { get; private set; }
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public NationalId? NationalId { get; private set; }
    public string? DepartmentId { get; private set; }
    public UserRole Role { get; private set; }
    public bool Active { get; private set; } = true;
    public string PasswordHash { get; private set; } = "";
    public string SecurityStamp { get; private set; } = Guid.NewGuid().ToString("N");
    public bool MustChangePassword { get; private set; } = true;
    public int FailedLogins { get; private set; }
    public DateTime? LockoutUntil { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private User() { } // EF Core

    public User(
        Username username,
        PersonnelCode personnelCode,
        string firstName,
        string lastName,
        NationalId? nationalId,
        string? departmentId,
        UserRole role,
        string passwordHash,
        bool mustChangePassword = true)
    {
        Username = username;
        PersonnelCode = personnelCode;
        FirstName = firstName;
        LastName = lastName;
        NationalId = nationalId;
        DepartmentId = departmentId;
        Role = role;
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        SecurityStamp = Guid.NewGuid().ToString("N");
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(
        PersonnelCode personnelCode,
        Username username,
        string firstName,
        string lastName,
        NationalId? nationalId,
        string? departmentId,
        UserRole role,
        bool active)
    {
        PersonnelCode = personnelCode;
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        NationalId = nationalId;
        DepartmentId = departmentId;
        Role = role;
        Active = active;
        SecurityStamp = Guid.NewGuid().ToString("N");
        IncrementVersion();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        MustChangePassword = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
        FailedLogins = 0;
        LockoutUntil = null;
        IncrementVersion();
    }

    public void ResetPassword(string newPasswordHash, string resetBy)
    {
        PasswordHash = newPasswordHash;
        MustChangePassword = true;
        SecurityStamp = Guid.NewGuid().ToString("N");
        FailedLogins = 0;
        LockoutUntil = null;
        IncrementVersion();
    }

    public void RecordFailedLogin()
    {
        FailedLogins++;
        if (FailedLogins >= 5)
        {
            LockoutUntil = DateTime.UtcNow.AddMinutes(15);
        }
        IncrementVersion();
    }

    public void ResetFailedLogins()
    {
        FailedLogins = 0;
        LockoutUntil = null;
        IncrementVersion();
    }

    public void Deactivate()
    {
        Active = false;
        SecurityStamp = Guid.NewGuid().ToString("N");
        IncrementVersion();
    }

    public string FullName => $"{FirstName} {LastName}";

    public bool IsLockedOut => LockoutUntil.HasValue && LockoutUntil > DateTime.UtcNow;
}