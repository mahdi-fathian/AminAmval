using FluentValidation;
using AminAmval.Application.DTOs;
using AminAmval.Domain.Enums;

namespace AminAmval.Application.Validators.Assets;

public sealed class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(60)
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.OldCode)
            .MaximumLength(60)
            .When(x => !string.IsNullOrEmpty(x.OldCode));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام اموال الزامی است.")
            .MaximumLength(200);

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("دسته‌بندی معتبر را انتخاب کنید.");

        RuleFor(x => x.Brand)
            .MaximumLength(100);

        RuleFor(x => x.Model)
            .MaximumLength(100);

        RuleFor(x => x.Serial)
            .MaximumLength(100);

        RuleFor(x => x.Quality)
            .Must(q => Enum.TryParse<AssetQuality>(q, true, out _))
            .WithMessage("کیفیت اموال معتبر نیست.");

        RuleFor(x => x.Owner)
            .NotEmpty().WithMessage("مالک اموال الزامی است.")
            .MaximumLength(150);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("توضیحات الزامی است.")
            .MaximumLength(2000);

        RuleFor(x => x.ImageId)
            .NotEmpty().WithMessage("بارگذاری تصویر اموال الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.PurchaseCost)
            .InclusiveBetween(0, 1_000_000_000_000_000L)
            .WithMessage("ارزش خرید باید بین صفر و ۱٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰ ریال باشد.")
            .When(x => x.PurchaseCost.HasValue);
    }
}

public sealed class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequest>
{
    public UpdateAssetRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد اموال الزامی است.")
            .MaximumLength(60);

        RuleFor(x => x.OldCode)
            .MaximumLength(60)
            .When(x => !string.IsNullOrEmpty(x.OldCode));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام اموال الزامی است.")
            .MaximumLength(200);

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("دسته‌بندی معتبر را انتخاب کنید.");

        RuleFor(x => x.Brand)
            .MaximumLength(100);

        RuleFor(x => x.Model)
            .MaximumLength(100);

        RuleFor(x => x.Serial)
            .MaximumLength(100);

        RuleFor(x => x.Quality)
            .Must(q => Enum.TryParse<AssetQuality>(q, true, out _))
            .WithMessage("کیفیت اموال معتبر نیست.");

        RuleFor(x => x.Owner)
            .NotEmpty().WithMessage("مالک اموال الزامی است.")
            .MaximumLength(150);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("توضیحات الزامی است.")
            .MaximumLength(2000);

        RuleFor(x => x.ImageId)
            .NotEmpty().WithMessage("بارگذاری تصویر اموال الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.PurchaseCost)
            .InclusiveBetween(0, 1_000_000_000_000_000L)
            .WithMessage("ارزش خرید باید بین صفر و ۱٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰٬۰۰۰ ریال باشد.")
            .When(x => x.PurchaseCost.HasValue);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class AssignAssetRequestValidator : AbstractValidator<AssignAssetRequest>
{
    public AssignAssetRequestValidator()
    {
        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("واحد سازمانی تحویل‌گیرنده مشخص نیست.");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("تاریخ شروع تخصیص الزامی است.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000);

        RuleFor(x => x.Reference)
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class TransferAssetRequestValidator : AbstractValidator<TransferAssetRequest>
{
    public TransferAssetRequestValidator()
    {
        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("واحد سازمانی تحویل‌گیرنده مشخص نیست.");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("تاریخ شروع انتقال الزامی است.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000);

        RuleFor(x => x.Reference)
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class ReturnAssetRequestValidator : AbstractValidator<ReturnAssetRequest>
{
    public ReturnAssetRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("تاریخ عودت الزامی است.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("علت عودت الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Reference)
            .MaximumLength(200);

        RuleFor(x => x.Location)
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class ChangeAssetStatusRequestValidator : AbstractValidator<ChangeAssetStatusRequest>
{
    public ChangeAssetStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("وضعیت الزامی است.")
            .Must(s => Enum.TryParse<AssetStatus>(s, true, out _))
            .WithMessage("وضعیت انتخابی معتبر نیست.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("تاریخ عملیات الزامی است.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("علت / شرح عملیات الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("شمارهٔ مجوز یا صورت‌جلسه الزامی است.")
            .MaximumLength(200);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("مبلغ مثبت فروش به ریال الزامی است.")
            .LessThanOrEqualTo(1_000_000_000_000_000L).WithMessage("مبلغ فروش بیش از حد مجاز است.")
            .When(x => x.Status == AssetStatus.Sold.ToString());

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class DispositionRequestRequestValidator : AbstractValidator<DispositionRequestRequest>
{
    public DispositionRequestRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("وضعیت الزامی است.")
            .Must(s => Enum.TryParse<AssetStatus>(s, true, out var status) && 
                (status == AssetStatus.Scrapped || status == AssetStatus.Sold || status == AssetStatus.Exited))
            .WithMessage("درخواست مجوز فقط برای فروش، اسقاط یا خروج است.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("تاریخ عملیات الزامی است.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("علت / شرح عملیات الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("شمارهٔ مجوز یا صورت‌جلسه الزامی است.")
            .MaximumLength(200);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("مبلغ مثبت فروش به ریال الزامی است.")
            .LessThanOrEqualTo(1_000_000_000_000_000L).WithMessage("مبلغ فروش بیش از حد مجاز است.")
            .When(x => x.Status == AssetStatus.Sold.ToString());

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class DispositionDecisionRequestValidator : AbstractValidator<DispositionDecisionRequest>
{
    public DispositionDecisionRequestValidator()
    {
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("توضیح تصمیم الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class ArchiveAssetRequestValidator : AbstractValidator<ArchiveAssetRequest>
{
    public ArchiveAssetRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("علت حذف الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class RestoreAssetRequestValidator : AbstractValidator<RestoreAssetRequest>
{
    public RestoreAssetRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("علت بازگردانی الزامی است.")
            .MaximumLength(1000);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}