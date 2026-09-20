using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Queries.Users;

public sealed record GetUserQuery(string Id) : IRequest<UserDto>;

public sealed record GetUsersQuery(UserFilterRequest Filter) : IRequest<PagedResult<UserListItemDto>>;

public sealed record GetCurrentUserQuery(string UserId) : IRequest<UserDto>;

public sealed record GetUserSessionsQuery(string UserId) : IRequest<IReadOnlyList<UserSessionDto>>;

public sealed record UserSessionDto(
    string Id,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime ExpiresAt,
    string Ip,
    string UserAgent,
    bool Current);