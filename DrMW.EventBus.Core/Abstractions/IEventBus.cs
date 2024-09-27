using DrMW.EventBus.Core.BaseModels;

namespace DrMW.EventBus.Core.Abstractions;

public interface IEventBus
{
    Task Publish(IntegrationEvent @event);

    Task Subscribe<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>;

    Task UnSubscribe<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>;
}