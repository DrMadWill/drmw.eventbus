namespace Dr.Rabbit.SubManager;

public class SubscriptionInfo
{
    public Type HandlerType { get; }

    public SubscriptionInfo(Type handleType)
    {
        HandlerType = handleType ?? throw new ArgumentException(nameof(handleType));
    }

    public static SubscriptionInfo Typed(Type handlerType)
    {
        return new SubscriptionInfo(handlerType);
    }
}