using DrMW.EventBus.Core.Abstractions;

namespace ConsoleAppTest.Models.TestQueEvents;

public class TestQueHandler : IIntegrationEventHandler<TestQueIntegrationEvent>
{
    
    public async Task Handle(TestQueIntegrationEvent @event)
    {
        Console.WriteLine("TestQueHandler worked {0} : EventId {1} : Event Time {2}",@event.Id,@event.IntegrationEventId,@event.IntegrationEventIdCreatedDate.ToString("G"));
     
    }
}