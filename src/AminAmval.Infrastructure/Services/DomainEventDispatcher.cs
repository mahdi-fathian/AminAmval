using AminAmval.Domain.Services;
using AminAmval.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace AminAmval.Infrastructure.Services;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(ILogger<DomainEventDispatcher> logger)
    {
        _logger = logger;
    }

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Domain event dispatched: {EventType} Id={EventId} at {OccurredAt}",
            domainEvent.GetType().Name, domainEvent.EventId, domainEvent.OccurredAt);
        return Task.CompletedTask;
    }

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var e in domainEvents)
            _ = DispatchAsync(e, cancellationToken);
        return Task.CompletedTask;
    }
}
