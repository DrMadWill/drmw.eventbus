using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Abstractions;

namespace DrMW.EventBus.RabbitMq.SubManagers;

public class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager
{
    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
    private readonly List<Type> _eventTypes;
    private static readonly object _lockObject = new object();
    public event EventHandler<string>? OnEventRemoved;

    public Func<string, string> EventNameGetter;

    public InMemoryEventBusSubscriptionManager(Func<string, string> eventNameGetter)
    {
        _handlers = new Dictionary<string, List<SubscriptionInfo>>();
        _eventTypes = new List<Type>();
        EventNameGetter = eventNameGetter;
    }

    public bool IsEmpty => !_handlers.Keys.Any();

    public void Clear()
    {
        foreach (var key in _handlers.Keys)
        {
            RaiseOnEventRemoved(key);
        }
        _handlers.Clear();
    }

    public void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        lock (_lockObject)
        {
            var eventName = GetEventKey<T>();
            AddSubscription(typeof(TH), eventName);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }
        }
    }

    private void AddSubscription(Type handlerType, string eventName)
    {
        if (!HasSubscriptionForEvent(eventName))
        {
            _handlers.Add(eventName, new List<SubscriptionInfo>());
        }

        if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
        {
            Console.WriteLine($" private void AddSubscription(Type handlerType, string eventName) => err | Handler Type {handlerType.Name} already registered for '{eventName}' ");
            throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'",
                nameof(handlerType));
        }
        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
    }

    public void RemoveSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        var handlerRoRemove = FindSubscriptionToRemove<T, TH>();
        var eventName = GetEventKey<T>();
        RemoveHandler(eventName, handlerRoRemove);
    }

    private void RemoveHandler(string eventName, SubscriptionInfo? subsToRemove)
    {
        if (subsToRemove != null)
        {
            _handlers[eventName].Remove(subsToRemove);
            if (!_handlers[eventName].Any())
            {
                _handlers.Remove(eventName);
                var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                if (eventType != null)
                {
                    _eventTypes.Remove(eventType);
                }

                RaiseOnEventRemoved(eventName);
            }
        }
    }

    public IEnumerable<SubscriptionInfo> GetHandlerForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return GetHandlerForEvent(key);
    }

    public IEnumerable<SubscriptionInfo> GetHandlerForEvent(string eventName) => _handlers[eventName];

    private void RaiseOnEventRemoved(string eventName)
    {
        var handler = OnEventRemoved;
        handler?.Invoke(this, eventName);
    }

    private SubscriptionInfo? FindSubscriptionToRemove<T, TH>()
        where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();
        return FindSubscriptionToRemove(eventName, typeof(TH));
    }

    private SubscriptionInfo? FindSubscriptionToRemove(string eventName, Type handlerType)
    {
        if (!HasSubscriptionForEvent(eventName)) return null;
        return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);
    }

    public bool HasSubscriptionForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();

        return HasSubscriptionForEvent(key);
    }

    public bool HasSubscriptionForEvent(string eventName) => _handlers.ContainsKey(eventName);

    public Type? GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(s => s.Name == eventName);

    public string GetEventKey<T>()
    {
        string eventName = typeof(T).Name;
        return EventNameGetter(eventName);
    }
}