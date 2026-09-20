using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Commands.Assets;

public sealed record CreateAssetCommand(CreateAssetRequest Request) : IRequest<string>;

public sealed record UpdateAssetCommand(string Id, UpdateAssetRequest Request) : IRequest<Unit>;

public sealed record ArchiveAssetCommand(string Id, ArchiveAssetRequest Request) : IRequest<Unit>;

public sealed record RestoreAssetCommand(string Id, RestoreAssetRequest Request) : IRequest<Unit>;

public sealed record AssignAssetCommand(string Id, AssignAssetRequest Request) : IRequest<string>;

public sealed record TransferAssetCommand(string Id, TransferAssetRequest Request) : IRequest<string>;

public sealed record ReturnAssetCommand(string Id, ReturnAssetRequest Request) : IRequest<Unit>;

public sealed record ChangeAssetStatusCommand(string Id, ChangeAssetStatusRequest Request) : IRequest<Unit>;

public sealed record RequestDispositionCommand(string Id, DispositionRequestRequest Request) : IRequest<string>;

public sealed record ApproveDispositionCommand(string Id, DispositionDecisionRequest Request) : IRequest<Unit>;

public sealed record RejectDispositionCommand(string Id, DispositionDecisionRequest Request) : IRequest<Unit>;

public sealed record CancelDispositionCommand(string Id, DispositionDecisionRequest Request) : IRequest<Unit>;

public sealed record UploadAssetImageCommand(byte[] ImageBytes, string UserId) : IRequest<string>;