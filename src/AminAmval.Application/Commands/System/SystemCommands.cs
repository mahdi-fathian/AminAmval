using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Commands.System;

public sealed record CreateBackupCommand : IRequest<string>;

public sealed record ImportExcelCommand(string Kind, byte[] FileBytes, bool Commit, string UserId) : IRequest<ImportResultDto>;

public sealed record GetImportTemplateCommand(string Kind) : IRequest<byte[]>;

public sealed record ExportAssetsCommand(string? Filters) : IRequest<byte[]>;

public sealed record ExportUsersCommand(string? Filters) : IRequest<byte[]>;

public sealed record ExportAuditCommand(string? Filters) : IRequest<byte[]>;

public sealed record ExportAssetHistoryCommand(string AssetId) : IRequest<byte[]>;

public sealed record CancelOperationCommand(string Operation, string? EntityId) : IRequest<Unit>;