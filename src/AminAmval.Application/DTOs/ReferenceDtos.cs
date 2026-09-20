namespace AminAmval.Application.DTOs;

public sealed record CategoryDto(
    string Id,
    string Name,
    string Description,
    int Version,
    int AssetCount);

public sealed record DepartmentDto(
    string Id,
    string Name,
    int Version,
    int UserCount,
    int ActiveAssetCount);

public sealed record CreateCategoryRequest(
    string Name,
    string Description);

public sealed record UpdateCategoryRequest(
    string Name,
    string Description,
    int Version);

public sealed record CreateDepartmentRequest(
    string Name);

public sealed record UpdateDepartmentRequest(
    string Name,
    int Version);