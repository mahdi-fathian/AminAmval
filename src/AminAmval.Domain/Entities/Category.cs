using AminAmval.Domain.Shared;

namespace AminAmval.Domain.Entities;

public sealed class Category : Entity
{
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";

    private Category() { } // EF Core

    public Category(string name, string description = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام دسته‌بندی نمی‌تواند خالی باشد.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? "";
    }

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام دسته‌بندی نمی‌تواند خالی باشد.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? "";
        IncrementVersion();
    }
}