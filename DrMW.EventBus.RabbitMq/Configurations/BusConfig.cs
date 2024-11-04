namespace DrMW.EventBus.RabbitMq.Configurations;

public class BusConfig
{
    public int ConnectionRetryCount { get; set; } = 5;
    public string DefaultTopicName { get; set; } = "Dr.Rabbit";
    public string EventBusConnectionString { get; set; } = string.Empty;
    public string SubscriberClientAppName { get; set; } = string.Empty;
    public string EventNamePrefix { get; set; } = string.Empty;
    public string EventNameSuffix { get; set; } = "IntegrationEvent";
    public string DeadLetterExchangeName { get; set; } = "dead-letter-exchange";
    public string DeadLetterQueueName { get; set; } = "dead-letter-queue";
    public object? Connection { get; set; }
    public string? ConnectionUrl { get; set; }
    public bool DeleteEventPrefix => !string.IsNullOrEmpty(EventNamePrefix);
    public bool DeleteEventSuffix => !string.IsNullOrEmpty(EventNameSuffix);
    public Func<string, string, Task>? OnDeadLetter { get; set; } = null;
}