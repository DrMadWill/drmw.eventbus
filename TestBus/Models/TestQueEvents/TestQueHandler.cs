using DrMW.EventBus.Core.Abstractions;

namespace TestBus.Models.TestQueEvents;

public class TestQueHandler : IIntegrationEventHandler<TestQueIntegrationEvent>
{
    
    public Task Handle(TestQueIntegrationEvent @event)
    {
        Console.WriteLine("TestQueHandler worked {0} : EventId {1} : Event Time {3}",@event.Id,@event.IntegrationEventId,@event.IntegrationEventIdCreatedDate.ToString("G"));
        return Task.CompletedTask;
    }
}