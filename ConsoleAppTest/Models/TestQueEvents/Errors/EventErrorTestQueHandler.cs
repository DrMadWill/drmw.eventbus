using DrMW.EventBus.Core.Abstractions;

namespace ConsoleAppTest.Models.TestQueEvents.Errors;

public class EventErrorTestQueHandler : IIntegrationEventHandler<EventErrorTestQueIntegrationEvent>
{
    
    public async Task Handle(EventErrorTestQueIntegrationEvent @event)
    {
        Console.WriteLine("TestQueHandler worked {0} : EventId {1} : Event Time {2}",@event.Id,@event.IntegrationEventId,@event.IntegrationEventIdCreatedDate.ToString("G"));
     
    }
}