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
        var extation  = 1;
        extation = extation;
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

    public async Task ProcessEvent(string eventName, string message)
    {
        eventName = ProcessEventName(eventName);
        if (!SubManager.HasSubscriptionForEvent(eventName))
            throw new Exception($"There is no subscription for this event. Please check your configuration. {eventName}");

        var subscriptions = SubManager.GetHandlerForEvent(eventName).ToArray();
        if(subscriptions.Length == 0)
            throw new Exception($"There is no subscription for this event. Please check your configuration. {eventName}");
        
        using var scope = _serviceProvider.CreateScope();
        foreach (var subscription in subscriptions)
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
       
    }

    public abstract Task Publish(IntegrationEvent @event);
    public abstract Task BasicPublishAsync(string message, string eventName);
    public abstract Task StartBasicConsumeAsync(string eventName, Func<string, string, Task> response);

    public abstract Task Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

    public abstract Task UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    
    
  
}