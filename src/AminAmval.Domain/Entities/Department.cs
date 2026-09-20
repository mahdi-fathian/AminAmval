using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class Department : Entity
{
    public string Name { get; private set; } = "";

    private Department() { } // EF Core

    public Department(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام واحد سازمانی نمی‌تواند خالی باشد.", nameof(name));

        Name = name.Trim();
    }

    public void Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام واحد سازمانی نمی‌تواند خالی باشد.", nameof(name));

        Name = name.Trim();
        IncrementVersion();
    }
}