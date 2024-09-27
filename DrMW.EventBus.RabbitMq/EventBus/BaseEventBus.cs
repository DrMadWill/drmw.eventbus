using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Configurations;
using DrMW.EventBus.RabbitMq.SubManager;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DrMW.EventBus.RabbitMq.EventBus;

public abstract class BaseEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    protected readonly IEventBusSubscriptionManager SubManager;
    private BusConfig _busConfig;

    public BaseEventBus(BusConfig config, IServiceProvider serviceProvider)
    {
        _busConfig = config;
        _serviceProvider = serviceProvider;
        SubManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
    }

    public virtual string ProcessEventName(string eventName)
    {
        if (_busConfig.DeleteEventPrefix && eventName.Contains(_busConfig.EventNamePrefix))
            eventName = eventName[(_busConfig.EventNamePrefix.Length - 1)..];
        if (_busConfig.DeleteEventSuffix && eventName.Contains(_busConfig.EventNameSuffix))
            eventName = eventName[..eventName.IndexOf(_busConfig.EventNameSuffix, StringComparison.Ordinal)];
        if (eventName.Contains(_busConfig.SubscriberClientAppName))
            eventName = eventName.Replace(_busConfig.SubscriberClientAppName + ".", "");
        return eventName;
    }

    public virtual string GetSubName(string eventName)
    {
        return $"{_busConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
    }

    public virtual void Dispose()
    {
        _busConfig = null;
    }

    public async Task<bool> ProcessEvent(string eventName, string message)
    {
        
        eventName = ProcessEventName(eventName);
        if (!SubManager.HasSubscriptionForEvent(eventName))
            return false;

        var subscriptions = SubManager.GetHandlerForEvent(eventName);
        using var scope = _serviceProvider.CreateScope();
        foreach (var subscription in subscriptions)
        {
            try
            {
                var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                if (handler == null)
                {
                    continue;
                }

                var eventType = SubManager.GetEventTypeByName($"{_busConfig.EventNamePrefix}{eventName}{_busConfig.EventNameSuffix}");
                var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                    
            }
        }

        return true;
    }

    public abstract Task Publish(IntegrationEvent @event);

    public abstract Task Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

    public abstract Task UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    
    
  
}