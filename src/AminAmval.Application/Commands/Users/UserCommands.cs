using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Commands.Users;

public sealed record CreateUserCommand(CreateUserRequest Request) : IRequest<string>;

public sealed record UpdateUserCommand(string Id, UpdateUserRequest Request) : IRequest<Unit>;

public sealed record ResetUserPasswordCommand(string Id, ResetPasswordRequest Request) : IRequest<Unit>;

public sealed record ChangePasswordCommand(ChangePasswordRequest Request) : IRequest<Unit>;

public sealed record RevokeSessionCommand(string SessionId) : IRequest<Unit>;

public sealed record RevokeAllSessionsCommand(string UserId) : IRequest<Unit>;