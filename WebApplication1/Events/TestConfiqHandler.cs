using MassTransit;

namespace WebApplication1.Events;

public class TestConfiqHandler(ILogger<TestConfiqHandler> logger) : IConsumer<TestConfiq>
{
    
    private readonly ILogger<TestConfiqHandler> _logger = logger;

    public async Task Consume(ConsumeContext<TestConfiq> context)
    {
        // Log the received message
        Console.WriteLine("TestConfiqHandler Consume method called");
        _logger.LogInformation("TestConfiqHandler Consume method called with message: {Message}", context.Message.Info);
    }
}