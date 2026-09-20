using MediatR;
using AminAmval.Domain.Entities;
using AminAmval.Domain.Enums;
using AminAmval.Domain.ValueObjects;
using AminAmval.Domain.Repositories;
using AminAmval.Domain.Exceptions;
using AminAmval.Domain.Events;
using AminAmval.Domain.Services;
using AminAmval.Application.Commands.Assets;
using AminAmval.Application.DTOs;
using AminAmval.Application.Exceptions;

namespace AminAmval.Application.Handlers.Assets;

public sealed class CreateAssetCommandHandler : IRequestHandler<CreateAssetCommand, string>
{
    private readonly IAssetRepository _assetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateAssetCommandHandler(
        IAssetRepository assetRepository,
        ICategoryRepository categoryRepository,
        IImageRepository imageRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _imageRepository = imageRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<string> Handle(CreateAssetCommand request, CancellationToken cancellationToken)
    {
        var input = request.Request;

        if (!await _categoryRepository.ExistsByNameAsync(input.CategoryId, cancellationToken: cancellationToken))
            throw new NotFoundException("دسته‌بندی معتبر را انتخاب کنید.");

        if (!string.IsNullOrEmpty(input.Code))
        {
            if (await _assetRepository.ExistsByCodeAsync(input.Code, cancellationToken: cancellationToken))
                throw new ConflictException("کد اموال تکراری است.");
        }

        if (!await _imageRepository.ExistsByIdAsync(input.ImageId, cancellationToken: cancellationToken))
            throw new NotFoundException("تصویر یافت نشد.");

        var code = string.IsNullOrEmpty(input.Code) 
            ? AssetCode.Generate() 
            : new AssetCode(input.Code);

        if (await _assetRepository.ExistsByCodeAsync(code.Value, cancellationToken: cancellationToken))
            throw new ConflictException("کد اموال تکراری است.");

        var quality = Enum.Parse<AssetQuality>(input.Quality, true);
        var purchaseCost = input.PurchaseCost.HasValue ? new Money(input.PurchaseCost.Value) : null;

        var asset = new Asset(
            code,
            input.Name,
            input.CategoryId,
            quality,
            input.Owner,
            input.Description,
            input.ImageId,
            input.Brand,
            input.Model,
            input.Serial,
            input.OldCode,
            input.PurchaseDate,
            purchaseCost,
            input.HasLabel,
            input.Location);

        await _assetRepository.AddAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetCreatedEvent(
            asset.Id, asset.Code, asset.Name, asset.Status, "system"), cancellationToken);

        return asset.Id;
    }
}

public sealed class UpdateAssetCommandHandler : IRequestHandler<UpdateAssetCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateAssetCommandHandler(
        IAssetRepository assetRepository,
        ICategoryRepository categoryRepository,
        IImageRepository imageRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _imageRepository = imageRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(UpdateAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        if (!await _categoryRepository.ExistsByNameAsync(input.CategoryId, cancellationToken: cancellationToken))
            throw new NotFoundException("دسته‌بندی معتبر را انتخاب کنید.");

        if (!await _imageRepository.ExistsByIdAsync(input.ImageId, cancellationToken: cancellationToken))
            throw new NotFoundException("تصویر یافت نشد.");

        var quality = Enum.Parse<AssetQuality>(input.Quality, true);
        var purchaseCost = input.PurchaseCost.HasValue ? new Money(input.PurchaseCost.Value) : null;

        asset.UpdateDetails(
            input.Name,
            input.CategoryId,
            quality,
            input.Owner,
            input.Description,
            input.ImageId,
            input.Brand,
            input.Model,
            input.Serial,
            input.OldCode,
            input.PurchaseDate,
            purchaseCost,
            input.HasLabel,
            input.Location);

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetUpdatedEvent(
            asset.Id, asset.Code, asset.Name, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class ArchiveAssetCommandHandler : IRequestHandler<ArchiveAssetCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ArchiveAssetCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(ArchiveAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        if (asset.Version != request.Request.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        asset.Archive(request.Request.Reason, "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetArchivedEvent(
            asset.Id, asset.Code, request.Request.Reason, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class RestoreAssetCommandHandler : IRequestHandler<RestoreAssetCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RestoreAssetCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(RestoreAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, includeArchived: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        if (asset.Version != request.Request.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        asset.Restore(request.Request.Reason, "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetRestoredEvent(
            asset.Id, asset.Code, request.Request.Reason, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class AssignAssetCommandHandler : IRequestHandler<AssignAssetCommand, string>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public AssignAssetCommandHandler(
        IAssetRepository assetRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<string> Handle(AssignAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        var department = await _departmentRepository.GetByIdAsync(input.DepartmentId, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("واحد سازمانی یافت نشد.");

        User? user = null;
        if (!string.IsNullOrEmpty(input.UserId))
        {
            user = await _userRepository.GetByIdAsync(input.UserId, cancellationToken: cancellationToken)
                ?? throw new NotFoundException("شخص انتخاب‌شده معتبر یا فعال نیست.");

            if (user.Role != UserRole.Employee)
                throw new BusinessRuleException("تخصیص اموال فقط به کارکنان سازمان مجاز است.");
        }

        asset.Assign(
            input.UserId ?? "",
            input.DepartmentId,
            input.StartedAt,
            input.Notes ?? "",
            input.Reference ?? "",
            "system",
            user == null ? department.Name : user.FullName,
            department.Name);

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var currentAssignment = asset.Assignments.First(a => a.EndedAt == null);
        
        await _eventDispatcher.DispatchAsync(new AssetAssignedEvent(
            asset.Id, asset.Code, input.UserId ?? "", input.DepartmentId, input.StartedAt, "system"), cancellationToken);

        return currentAssignment.Id;
    }
}

public sealed class TransferAssetCommandHandler : IRequestHandler<TransferAssetCommand, string>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public TransferAssetCommandHandler(
        IAssetRepository assetRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<string> Handle(TransferAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        var department = await _departmentRepository.GetByIdAsync(input.DepartmentId, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("واحد سازمانی یافت نشد.");

        User? user = null;
        if (!string.IsNullOrEmpty(input.UserId))
        {
            user = await _userRepository.GetByIdAsync(input.UserId, cancellationToken: cancellationToken)
                ?? throw new NotFoundException("شخص انتخاب‌شده معتبر یا فعال نیست.");

            if (user.Role != UserRole.Employee)
                throw new BusinessRuleException("تخصیص اموال فقط به کارکنان سازمان مجاز است.");
        }

        var currentAssignment = asset.Assignments.FirstOrDefault(a => a.EndedAt == null);
        var fromUserId = currentAssignment?.UserId;

        asset.Transfer(
            input.UserId ?? "",
            input.DepartmentId,
            input.StartedAt,
            input.Notes ?? "",
            input.Reference ?? "",
            "system",
            user == null ? department.Name : user.FullName,
            department.Name);

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var newAssignment = asset.Assignments.First(a => a.EndedAt == null);

        await _eventDispatcher.DispatchAsync(new AssetTransferredEvent(
            asset.Id, asset.Code, fromUserId ?? "", input.UserId ?? "", input.DepartmentId, input.StartedAt, "system"), cancellationToken);

        return newAssignment.Id;
    }
}

public sealed class ReturnAssetCommandHandler : IRequestHandler<ReturnAssetCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ReturnAssetCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(ReturnAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        var currentAssignment = asset.Assignments.FirstOrDefault(a => a.EndedAt == null)
            ?? throw new BusinessRuleException("اموال تخصیص جاری ندارد.");

        asset.Return(
            input.Date,
            input.Reason,
            input.Reference ?? "",
            input.Location ?? "",
            "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetReturnedEvent(
            asset.Id, asset.Code, currentAssignment.UserId ?? "", input.Date, input.Reason, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class ChangeAssetStatusCommandHandler : IRequestHandler<ChangeAssetStatusCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ChangeAssetStatusCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(ChangeAssetStatusCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        var newStatus = Enum.Parse<AssetStatus>(input.Status, true);
        var amount = input.Amount.HasValue ? new Money(input.Amount.Value) : null;

        var oldStatus = asset.Status;
        asset.ChangeStatus(newStatus, input.Date, input.Reason, input.Reference, amount, "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventDispatcher.DispatchAsync(new AssetStatusChangedEvent(
            asset.Id, asset.Code, oldStatus, newStatus, input.Date, input.Reason, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class RequestDispositionCommandHandler : IRequestHandler<RequestDispositionCommand, string>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RequestDispositionCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<string> Handle(RequestDispositionCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        var input = request.Request;

        if (asset.Version != input.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        var targetStatus = Enum.Parse<AssetStatus>(input.Status, true);
        var amount = input.Amount.HasValue ? new Money(input.Amount.Value) : null;

        asset.RequestDisposition(targetStatus, input.Date, input.Reason, input.Reference, amount, "system", "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requestEntity = asset.DispositionRequests.First(r => r.State == DispositionState.Pending);

        await _eventDispatcher.DispatchAsync(new DispositionRequestedEvent(
            requestEntity.Id, asset.Id, asset.Code, targetStatus, input.Date, "system"), cancellationToken);

        return requestEntity.Id;
    }
}

public sealed class ApproveDispositionCommandHandler : IRequestHandler<ApproveDispositionCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ApproveDispositionCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(ApproveDispositionCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        if (asset.Version != request.Request.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        asset.ApproveDisposition(request.Id, "system", asset.Code.Value);

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requestEntity = asset.DispositionRequests.First(r => r.Id == request.Id);

        await _eventDispatcher.DispatchAsync(new DispositionApprovedEvent(
            requestEntity.Id, asset.Id, asset.Code, requestEntity.TargetStatus, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class RejectDispositionCommandHandler : IRequestHandler<RejectDispositionCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RejectDispositionCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(RejectDispositionCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        if (asset.Version != request.Request.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تaze‌سازی کنید.");

        asset.RejectDisposition(request.Id, "system", request.Request.Note);

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requestEntity = asset.DispositionRequests.First(r => r.Id == request.Id);

        await _eventDispatcher.DispatchAsync(new DispositionRejectedEvent(
            requestEntity.Id, asset.Id, asset.Code, "system", request.Request.Note), cancellationToken);

        return Unit.Value;
    }
}

public sealed class CancelDispositionCommandHandler : IRequestHandler<CancelDispositionCommand, Unit>
{
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CancelDispositionCommandHandler(
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<Unit> Handle(CancelDispositionCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetByIdAsync(request.Id, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("اموال مورد نظر پیدا نشد.");

        if (asset.Version != request.Request.Version)
            throw new ConflictException("اطلاعات هم‌زمان تغییر کرده است. صفحه را تازه‌سازی کنید.");

        asset.CancelDisposition(request.Id, "system");

        await _assetRepository.UpdateAsync(asset, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requestEntity = asset.DispositionRequests.First(r => r.Id == request.Id);

        await _eventDispatcher.DispatchAsync(new DispositionCancelledEvent(
            requestEntity.Id, asset.Id, asset.Code, "system"), cancellationToken);

        return Unit.Value;
    }
}

public sealed class UploadAssetImageCommandHandler : IRequestHandler<UploadAssetImageCommand, string>
{
    private readonly IImageStorageService _imageStorage;
    private readonly IImageRepository _imageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UploadAssetImageCommandHandler(
        IImageStorageService imageStorage,
        IImageRepository imageRepository,
        IUnitOfWork unitOfWork)
    {
        _imageStorage = imageStorage;
        _imageRepository = imageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(UploadAssetImageCommand request, CancellationToken cancellationToken)
    {
        var image = await _imageStorage.StoreAsync(request.ImageBytes, request.UserId, cancellationToken);
        await _imageRepository.AddAsync(image, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return image.Id;
    }
}