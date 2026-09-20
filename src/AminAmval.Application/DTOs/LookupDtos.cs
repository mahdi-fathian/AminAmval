namespace AminAmval.Application.DTOs;

public sealed record LookupsDto(
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<DepartmentDto> Departments,
    IReadOnlyList<UserLookupDto> Users,
    IReadOnlyList<string> Owners);

public sealed record UserLookupDto(
    string Id,
    string Name,
    string PersonnelCode,
    string DepartmentId,
    string Role);

public sealed record AssetFilterRequest(
    bool Archived,
    string? Query,
    string? CategoryId,
    string? Status,
    string? UserId,
    string? DepartmentId,
    string? Quality,
    string? Owner,
    string? Report,
    bool? HasLabel,
    DateTime? PurchaseFrom,
    DateTime? PurchaseTo,
    string? Sort,
    int Page,
    int PageSize);

public sealed record UserFilterRequest(
    string? Query,
    string? DepartmentId,
    string? Role,
    bool? Active,
    int Page,
    int PageSize);

public sealed record OperationFilterRequest(
    string? State,
    string? Query,
    int Page,
    int PageSize);

public sealed record ImportPreviewDto(
    bool CanCommit,
    int Total,
    int Valid,
    IReadOnlyList<ImportErrorDto> Errors,
    bool Committed,
    IReadOnlyList<ImportPreviewRowDto> Preview);

public sealed record ImportErrorDto(int Row, string Message);

public sealed record ImportPreviewRowDto(
    int Row,
    string Name,
    string Code,
    string Reference);

public sealed record ImportResultDto(
    bool Committed,
    int Total,
    int Valid,
    bool CanCommit,
    IReadOnlyList<ImportErrorDto> Errors,
    string? Message);