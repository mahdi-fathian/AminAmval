using AminAmval.Domain.Enums;
using AminAmval.Domain.ValueObjects;

namespace AminAmval.Domain.Exceptions;

public sealed class InvalidMoneyAmountException : DomainException
{
    public long AttemptedAmount { get; }

    public InvalidMoneyAmountException(long attemptedAmount, string message) : base(message)
    {
        AttemptedAmount = attemptedAmount;
    }
}

public sealed class InvalidNationalIdException : DomainException
{
    public string AttemptedValue { get; }

    public InvalidNationalIdException(string attemptedValue, string message) : base(message)
    {
        AttemptedValue = attemptedValue;
    }
}

public sealed class DuplicateAssetCodeException : DomainException
{
    public AssetCode AssetCode { get; }

    public DuplicateAssetCodeException(AssetCode assetCode) : base($"کد اموال '{assetCode}' تکراری است.")
    {
        AssetCode = assetCode;
    }
}

public sealed class AssetNotFoundException : DomainException
{
    public string AssetId { get; }

    public AssetNotFoundException(string assetId) : base($"اموال با شناسه '{assetId}' یافت نشد.")
    {
        AssetId = assetId;
    }
}

public sealed class InvalidAssetStateTransitionException : DomainException
{
    public AssetStatus CurrentStatus { get; }
    public AssetStatus RequestedStatus { get; }

    public InvalidAssetStateTransitionException(AssetStatus current, AssetStatus requested, string message)
        : base(message)
    {
        CurrentStatus = current;
        RequestedStatus = requested;
    }
}

public sealed class AssetHasActiveAssignmentException : DomainException
{
    public string AssetId { get; }

    public AssetHasActiveAssignmentException(string assetId) : base("این مال تخصیص فعال دارد و قابل حذف یا تغییر وضعیت نیست.")
    {
        AssetId = assetId;
    }
}

public sealed class AssetArchivedException : DomainException
{
    public string AssetId { get; }

    public AssetArchivedException(string assetId) : base("این مال بایگانی شده است. ابتدا از بایگانی خارج کنید.")
    {
        AssetId = assetId;
    }
}

public sealed class AssetHasPendingDispositionRequestException : DomainException
{
    public string AssetId { get; }

    public AssetHasPendingDispositionRequestException(string assetId) : base("این مال درخواست مجوز در انتظار دارد.")
    {
        AssetId = assetId;
    }
}

public sealed class UserNotFoundException : DomainException
{
    public string UserId { get; }

    public UserNotFoundException(string userId) : base($"کاربر با شناسه '{userId}' یافت نشد.")
    {
        UserId = userId;
    }
}

public sealed class DuplicatePersonnelCodeException : DomainException
{
    public PersonnelCode PersonnelCode { get; }

    public DuplicatePersonnelCodeException(PersonnelCode code) : base($"کد پرسنلی '{code}' تکراری است.")
    {
        PersonnelCode = code;
    }
}

public sealed class DuplicateUsernameException : DomainException
{
    public Username Username { get; }

    public DuplicateUsernameException(Username username) : base($"نام کاربری '{username}' تکراری است.")
    {
        Username = username;
    }
}

public sealed class DuplicateNationalIdException : DomainException
{
    public NationalId NationalId { get; }

    public DuplicateNationalIdException(NationalId nationalId) : base($"کد ملی '{nationalId}' تکراری است.")
    {
        NationalId = nationalId;
    }
}

public sealed class CannotDeactivateSelfException : DomainException
{
    public CannotDeactivateSelfException() : base("غیرفعال کردن حساب خود مجاز نیست.") { }
}

public sealed class LastAdminCannotBeDeactivatedException : DomainException
{
    public LastAdminCannotBeDeactivatedException() : base("سامانه باید حداقل یک مدیر فعال داشته باشد.") { }
}

public sealed class UserHasActiveAssignmentsException : DomainException
{
    public string UserId { get; }

    public UserHasActiveAssignmentsException(string userId) : base("پیش از غیرفعال‌سازی، اموال تحویلی کاربر را عودت یا انتقال دهید.")
    {
        UserId = userId;
    }
}

public sealed class InvalidPasswordException : DomainException
{
    public InvalidPasswordException(string message) : base(message) { }
}

public sealed class CurrentPasswordIncorrectException : DomainException
{
    public CurrentPasswordIncorrectException() : base("گذرواژهٔ فعلی صحیح نیست.") { }
}

public sealed class PasswordSameAsCurrentException : DomainException
{
    public PasswordSameAsCurrentException() : base("گذرواژهٔ جدید باید با گذرواژهٔ فعلی متفاوت باشد.") { }
}

public sealed class PasswordSameAsNationalIdException : DomainException
{
    public PasswordSameAsNationalIdException() : base("از کد ملی به‌عنوان گذرواژهٔ جدید استفاده نکنید.") { }
}

public sealed class CategoryNotFoundException : DomainException
{
    public string CategoryId { get; }

    public CategoryNotFoundException(string categoryId) : base($"دسته‌بندی با شناسه '{categoryId}' یافت نشد.")
    {
        CategoryId = categoryId;
    }
}

public sealed class CategoryHasAssetsException : DomainException
{
    public string CategoryId { get; }

    public CategoryHasAssetsException(string categoryId) : base("دسته‌بندی دارای اموال است و قابل حذف نیست.")
    {
        CategoryId = categoryId;
    }
}

public sealed class DepartmentNotFoundException : DomainException
{
    public string DepartmentId { get; }

    public DepartmentNotFoundException(string departmentId) : base($"واحد سازمانی با شناسه '{departmentId}' یافت نشد.")
    {
        DepartmentId = departmentId;
    }
}

public sealed class DepartmentHasUsersOrAssignmentsException : DomainException
{
    public string DepartmentId { get; }

    public DepartmentHasUsersOrAssignmentsException(string departmentId) : base("واحد سازمانی دارای کاربر یا سابقهٔ تخصیص است و قابل حذف نیست.")
    {
        DepartmentId = departmentId;
    }
}

public sealed class InvalidDispositionStateException : DomainException
{
    public DispositionState CurrentState { get; }

    public InvalidDispositionStateException(DispositionState currentState, string message) : base(message)
    {
        CurrentState = currentState;
    }
}

public sealed class CannotApproveOwnRequestException : DomainException
{
    public CannotApproveOwnRequestException() : base("تأیید درخواست توسط ثبت‌کننده مجاز نیست؛ یک مدیر دیگر باید آن را بررسی کند.") { }
}

public sealed class ConcurrencyException : DomainException
{
    public string EntityType { get; }
    public string EntityId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public ConcurrencyException(string entityType, string entityId, int expectedVersion, int actualVersion)
        : base($"اطلاعات هم‌زمان تغییر کرده است. نسخه مورد انتظار: {expectedVersion}, نسخه فعلی: {actualVersion}")
    {
        EntityType = entityType;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}