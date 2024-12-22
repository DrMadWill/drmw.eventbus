using System.Text;
using DrMW.EventBus.Core.Abstractions;
 
namespace ConsoleAppTest.Models.EventErrors;

public class GlobalErrorHandling : IIntegrationEventHandler<DeadLetterQueIntegrationEvent>
{
    public async Task Handle(DeadLetterQueIntegrationEvent @event)
    {
        var failMessage = Convert.FromBase64String(@event.Base64Message);
        var message = Encoding.UTF8.GetString(failMessage);
        
        Console.WriteLine("GlobalErrorHandling FailMessage {0} : EventId {1} : Event Time {2}",message ?? "",@event.IntegrationEventId,@event.IntegrationEventIdCreatedDate.ToString("G"));
    }
}