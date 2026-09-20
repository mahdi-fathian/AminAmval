using AminAmval.Domain.Enums;

namespace AminAmval.Application.DTOs;

public sealed record UserDto(
    string Id,
    string Username,
    string PersonnelCode,
    string FirstName,
    string LastName,
    string FullName,
    string? NationalId,
    string DepartmentId,
    string DepartmentName,
    string Role,
    bool Active,
    bool MustChangePassword,
    int Version,
    DateTime CreatedAt,
    int CurrentAssetCount);

public sealed record UserListItemDto(
    string Id,
    string Username,
    string PersonnelCode,
    string FirstName,
    string LastName,
    string FullName,
    string? NationalId,
    string DepartmentId,
    string DepartmentName,
    string Role,
    bool Active,
    int Version,
    DateTime CreatedAt,
    int CurrentAssetCount);

public sealed record CreateUserRequest(
    string PersonnelCode,
    string Username,
    string FirstName,
    string LastName,
    string NationalId,
    string DepartmentId,
    string Role,
    string? Password);

public sealed record UpdateUserRequest(
    string PersonnelCode,
    string Username,
    string FirstName,
    string LastName,
    string NationalId,
    string DepartmentId,
    string Role,
    bool Active,
    int Version);

public sealed record ResetPasswordRequest(
    string NewPassword);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);