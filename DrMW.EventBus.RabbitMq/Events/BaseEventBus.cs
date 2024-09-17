using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Abstractions;
using DrMW.EventBus.RabbitMq.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DrMW.EventBus.RabbitMq.Events;

public abstract class BaseEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    protected readonly IEventBusSubscriptionManager SubManager;
    protected EventBusConfig EventBusConfig;
    private bool _isLog = false;

    public BaseEventBus(EventBusConfig config, IServiceProvider serviceProvider,bool isLog)
    {
        EventBusConfig = config;
        _serviceProvider = serviceProvider;
        SubManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
        _isLog = isLog;
    }

    public virtual string ProcessEventName(string eventName)
    {
        if (EventBusConfig.DeleteEventPrefix && eventName.Contains(EventBusConfig.EventNamePrefix))
            eventName = eventName[(EventBusConfig.EventNamePrefix.Length - 1)..];
        if (EventBusConfig.DeleteEventSuffix && eventName.Contains(EventBusConfig.EventNameSuffix))
            eventName = eventName[..eventName.IndexOf(EventBusConfig.EventNameSuffix, StringComparison.Ordinal)];
        if (eventName.Contains(EventBusConfig.SubscriberClientAppName))
            eventName = eventName.Replace(EventBusConfig.SubscriberClientAppName + ".", "");
        return eventName;
    }

    public virtual string GetSubName(string eventName)
    {
        return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
    }

    public virtual void Dispose()
    {
        EventBusConfig = null;
    }

    public async Task<bool> ProcessEvent(string eventName, string message)
    {
        
        eventName = ProcessEventName(eventName);
        Log(nameof(ProcessEvent),$"{eventName} stared ...");
        if (!SubManager.HasSubscriptionForEvent(eventName))
        {
            Log(nameof(ProcessEvent),"HasSubscriptionForEvent in not found",$"{eventName} end ...");
            return false;
        }

        var subscriptions = SubManager.GetHandlerForEvent(eventName);
        using (var scope = _serviceProvider.CreateScope())
        {
            foreach (var subscription in subscriptions)
            {
                try
                {
                    Log(nameof(ProcessEvent),$"handler created ...");
                    var handler = _serviceProvider.GetService(subscription.HandlerType);
                    if (handler == null)
                    {
                        Log(nameof(ProcessEvent),$"handler is null ...");
                        continue;
                    }

                    var eventType =
                        SubManager.GetEventTypeByName(
                            $"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                    var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    Log(nameof(ProcessEvent),$"installation method ...");
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    Log(nameof(ProcessEvent),$"handler end ...");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    
                }
            }
        }
        return true;
    }

    public abstract void Publish(IntegrationEvent @event);

    public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

    public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    
    
    private void Log(params string[] logs)
    {
        if (_isLog)
        {
            Console.WriteLine("BaseEventBus >>>>>>>>>>>>> : " +string.Join(" | ",logs));
        }
    }
}