using Newtonsoft.Json;

namespace DrMW.EventBus.Core.BaseModels;

public class IntegrationEvent
{
    [JsonProperty]
    public Guid  IntegrationEventId { get; private set; }

    [JsonProperty]
    public DateTime IntegrationEventIdCreatedDate { get; private set; }

    public IntegrationEvent()
    {
        IntegrationEventId = Guid.NewGuid();
        IntegrationEventIdCreatedDate = DateTime.UtcNow;
    }

    [JsonConstructor]
    public IntegrationEvent(Guid id, DateTime createdDate)
    {
        IntegrationEventId = id;
        IntegrationEventIdCreatedDate = createdDate;
    }
}

