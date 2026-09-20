namespace AminAmval.Domain.Shared;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
    string EventId { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
}