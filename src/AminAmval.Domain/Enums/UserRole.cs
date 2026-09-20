namespace AminAmval.Domain.Enums;

public enum UserRole
{
    Employee = 1,
    Custodian = 2,
    Admin = 3
}

public static class UserRoleExtensions
{
    public static string ToPersian(this UserRole role) => role switch
    {
        UserRole.Admin => "مدیر سامانه",
        UserRole.Custodian => "جمعدار اموال",
        UserRole.Employee => "کاربر عادی",
        _ => role.ToString()
    };

    public static UserRole FromString(string role) => role switch
    {
        "Admin" => UserRole.Admin,
        "Custodian" => UserRole.Custodian,
        "Employee" => UserRole.Employee,
        _ => throw new ArgumentException($"نقش نامعتبر: {role}")
    };
}