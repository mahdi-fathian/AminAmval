using FluentValidation;
using AminAmval.Application.DTOs;
using AminAmval.Domain.Enums;

namespace AminAmval.Application.Validators.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.PersonnelCode)
            .NotEmpty().WithMessage("کد پرسنلی الزامی است.")
            .MaximumLength(60)
            .Matches("^[A-Z0-9\\-]{3,60}$").WithMessage("کد پرسنلی فقط می‌تواند شامل حروف لاتین، ارقام و خط تیره باشد.");

        RuleFor(x => x.Username)
            .MaximumLength(60)
            .When(x => !string.IsNullOrEmpty(x.Username));

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("نام خانوادگی الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage("کد ملی الزامی است.")
            .Length(10).WithMessage("کد ملی باید ۱۰ رقم باشد.")
            .Matches("^[0-9]{10}$").WithMessage("کد ملی باید فقط شامل ارقام باشد.");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("واحد سازمانی معتبر را انتخاب کنید.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("نقش الزامی است.")
            .Must(r => Enum.TryParse<UserRole>(r, true, out _))
            .WithMessage("نقش نامعتبر است.");

        RuleFor(x => x.Password)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.PersonnelCode)
            .NotEmpty().WithMessage("کد پرسنلی الزامی است.")
            .MaximumLength(60)
            .Matches("^[A-Z0-9\\-]{3,60}$").WithMessage("کد پرسنلی فقط می‌تواند شامل حروف لاتین، ارقام و خط تیره باشد.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("نام کاربری الزامی است.")
            .MaximumLength(60)
            .Matches("^[a-z0-9][a-z0-9._-]{2,59}$").WithMessage("نام کاربری باید ۳ تا ۶۰ کاراکتر باشد و فقط شامل حروف لاتین کوچک، ارقام، نقطه، خط تیره و زیرخط می‌باشد.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("نام الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("نام خانوادگی الزامی است.")
            .MaximumLength(100);

        RuleFor(x => x.NationalId)
            .NotEmpty().WithMessage("کد ملی الزامی است.")
            .Length(10).WithMessage("کد ملی باید ۱۰ رقم باشد.")
            .Matches("^[0-9]{10}$").WithMessage("کد ملی باید فقط شامل ارقام باشد.");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("واحد سازمانی معتبر را انتخاب کنید.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("نقش الزامی است.")
            .Must(r => Enum.TryParse<UserRole>(r, true, out _))
            .WithMessage("نقش نامعتبر است.");

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("نسخه نامعتبر است.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("گذرواژه جدید الزامی است.")
            .MinimumLength(12).WithMessage("گذرواژه باید حداقل ۱۲ کاراکتر باشد.")
            .MaximumLength(128).WithMessage("گذرواژه نباید بیش از ۱۲۸ کاراکتر باشد.")
            .Matches("[a-z]").WithMessage("گذرواژه باید شامل حروف کوچک لاتین باشد.")
            .Matches("[A-Z]").WithMessage("گذرواژه باید شامل حروف بزرگ لاتین باشد.")
            .Matches("[0-9]").WithMessage("گذرواژه باید شامل ارقام باشد.")
            .Matches("[^a-zA-Z0-9]").WithMessage("گذرواژه باید شامل نمادها باشد.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("گذرواژهٔ فعلی الزامی است.")
            .MaximumLength(128);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("گذرواژه جدید الزامی است.")
            .MinimumLength(12).WithMessage("گذرواژه باید حداقل ۱۲ کاراکتر باشد.")
            .MaximumLength(128).WithMessage("گذرواژه نباید بیش از ۱۲۸ کاراکتر باشد.")
            .Matches("[a-z]").WithMessage("گذرواژه باید شامل حروف کوچک لاتین باشد.")
            .Matches("[A-Z]").WithMessage("گذرواژه باید شامل حروف بزرگ لاتین باشد.")
            .Matches("[0-9]").WithMessage("گذرواژه باید شامل ارقام باشد.")
            .Matches("[^a-zA-Z0-9]").WithMessage("گذرواژه باید شامل نمادها باشد.");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("گذرواژهٔ جدید باید با گذرواژهٔ فعلی متفاوت باشد.");
    }
}