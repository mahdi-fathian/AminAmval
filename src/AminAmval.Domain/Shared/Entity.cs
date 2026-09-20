using System;

namespace AminAmval.Domain.Shared;

public abstract class Entity
{
    public string Id { get; protected set; } = Guid.NewGuid().ToString("N");
    public int Version { get; protected set; } = 1;

    protected Entity() { }

    protected Entity(string id)
    {
        Id = id;
    }

    public void IncrementVersion() => Version++;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? a, Entity? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(Entity? a, Entity? b) => !(a == b);
}