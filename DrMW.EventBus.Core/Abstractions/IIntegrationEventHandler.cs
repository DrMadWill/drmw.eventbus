using DrMW.EventBus.Core.BaseModels;

namespace DrMW.EventBus.Core.Abstractions;

// ReSharper disable once InconsistentNaming
public interface IIntegrationEventHandler<TIntegrationEvent> : IntegrationEventHandler
where TIntegrationEvent : IntegrationEvent
{
    Task Handle(TIntegrationEvent @event);
}

public interface IntegrationEventHandler
{
}