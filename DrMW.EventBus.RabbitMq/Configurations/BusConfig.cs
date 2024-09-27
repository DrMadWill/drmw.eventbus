namespace DrMW.EventBus.RabbitMq.Configurations;

public class BusConfig
{
    public int ConnectionRetryCount { get; set; } = 5;
    public string DefaultTopicName { get; set; } = "Dr.Rabbit";
    public string EventBusConnectionString { get; set; } = string.Empty;
    public string SubscriberClientAppName { get; set; } = string.Empty;
    public string EventNamePrefix { get; set; } = string.Empty;
    public string EventNameSuffix { get; set; } = "IntegrationEvent";
    public object? Connection { get; set; }
    public string? ConnectionUrl { get; set; }
    public bool DeleteEventPrefix => !string.IsNullOrEmpty(EventNamePrefix);
    public bool DeleteEventSuffix => !string.IsNullOrEmpty(EventNameSuffix);
}