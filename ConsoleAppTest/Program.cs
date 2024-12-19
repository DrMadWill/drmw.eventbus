// See https://aka.ms/new-console-template for more information

using ConsoleAppTest.Models.TestQueEvents;
using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using TestBus.Models.TestQueEvents;

Console.WriteLine("Hello, World!");
IServiceCollection _serviceCollection;
IEventBus _bus;

_serviceCollection = new ServiceCollection();
_serviceCollection.AddScoped<TestQueHandler>();
_serviceCollection.AddRabbitMq("Test.Que", "amqp://guest:guest@localhost:5672");
var app = _serviceCollection.BuildServiceProvider();
_bus = app.GetService<IEventBus>();
await _bus.Subscribe<TestQueIntegrationEvent, TestQueHandler>();


for (int i = 1; i <= 1; i++)
{
    await _bus.Publish(new TestQueIntegrationEvent
    {
        Id = i
    });
}

Console.ReadKey();