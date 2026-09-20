using FluentValidation;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Validators.References;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام دسته‌بندی الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام دسته‌بندی الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام واحد سازمانی الزامی است.")
            .MaximumLength(100);
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام واحد سازمانی الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}