using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Queries.Assets;

public sealed record GetAssetQuery(string Id, bool IsStaff) : IRequest<AssetDto>;

public sealed record GetAssetsQuery(AssetFilterRequest Filter, bool IsStaff) : IRequest<PagedResult<AssetListItemDto>>;

public sealed record GetAssetHistoryQuery(string Id, int Page, int PageSize, long? BeforeId) : IRequest<PagedResult<AuditEventDto>>;

public sealed record GetAssetLabelQuery(string Id) : IRequest<string>;

public sealed record GetAssetReceiptQuery(string AssignmentId, bool IsReturn) : IRequest<string>;