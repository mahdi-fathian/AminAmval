using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Queries.System;

public sealed record GetDashboardQuery(bool IsStaff, bool IsAdmin) : IRequest<DashboardDto>;

public sealed record GetAuditQuery(AuditFilterRequest Filter) : IRequest<PagedResult<AuditEventDto>>;

public sealed record GetSystemInfoQuery : IRequest<SystemInfoDto>;

public sealed record GetBackupFilesQuery : IRequest<IReadOnlyList<BackupFileDto>>;