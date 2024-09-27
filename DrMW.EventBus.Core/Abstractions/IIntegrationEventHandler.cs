using DrMW.EventBus.Core.BaseModels;

namespace DrMW.EventBus.Core.Abstractions;

public interface IIntegrationEventHandler<in TEvent> 
    where TEvent : IntegrationEvent
{
    Task Handle(TEvent @event);
}