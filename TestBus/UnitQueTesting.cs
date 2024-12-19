using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using TestBus.Models.TestQueEvents;

namespace TestBus;

public class UnitQueTesting
{

    private readonly IServiceCollection _serviceCollection;
    private readonly IEventBus _bus;

    public UnitQueTesting()
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddRabbitMq("Test.Que", "amqp://guest:guest@localhost:5672");
        var app = _serviceCollection.BuildServiceProvider();
        _bus = app.GetService<IEventBus>();
        _serviceCollection.AddScoped<TestQueHandler>();
        _bus.Subscribe<TestQueIntegrationEvent, TestQueHandler>().GetAwaiter();
        
    }
    
    [Fact]
    public void CheckQueSendingAndAcceptingCount()
    {
        for (int i = 1; i <= 15; i++)
        {
            _bus.Publish(new TestQueIntegrationEvent
            {
                Id = i
            }).GetAwaiter();
        }
    }
    
    [Fact]
    public void CheckQueSendingAndAcceptingContent()
    {
        
    }
    
    [Fact]
    public void CheckQueDeadLetter()
    {
        
    }
    
    
    
}