using MassTransit;

namespace ConsoleAppTe;

public class TestQueHandler : IConsumer<TestQue>
{

    public Task Consume(ConsumeContext<TestQue> context)
    {
        var @event = context.Message;
        Console.WriteLine("TestQueHandler worked {0} : EventId {1} : Event Time {2}", @event.Id, DateTime.Now,
            context.RequestId);
        return Task.CompletedTask;
    }
}