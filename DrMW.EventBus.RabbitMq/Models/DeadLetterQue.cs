using DrMW.EventBus.Core.BaseModels;

namespace DrMW.EventBus.RabbitMq.Models;

public class DeadLetterQue : IntegrationEvent
{
    public string? Base64Message { get; set; }
    public string? EventName { get; set; }
    public string? AppName { get; set; }
    public string? Prefix { get; set; }
}