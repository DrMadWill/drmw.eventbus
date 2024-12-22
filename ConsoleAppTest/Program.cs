// See https://aka.ms/new-console-template for more information

using ConsoleAppTest.Models.EventErrors;
using ConsoleAppTest.Models.TestQueEvents;
using ConsoleAppTest.Models.TestQueEvents.Errors;
using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello, World!");
IServiceCollection _serviceCollection;
IEventBus _bus;

_serviceCollection = new ServiceCollection();
_serviceCollection.AddScoped<TestQueHandler>();
_serviceCollection.AddScoped<EventErrorTestQueHandler>();
_serviceCollection.AddScoped<GlobalErrorHandling>();
_serviceCollection.AddRabbitMq("Test.Que", "amqp://guest:guest@localhost:5672");
var app = _serviceCollection.BuildServiceProvider();
_bus = app.GetService<IEventBus>();
await _bus.Subscribe<TestQueIntegrationEvent, TestQueHandler>();
await _bus.Subscribe<EventErrorTestQueIntegrationEvent, EventErrorTestQueHandler>();
await _bus.Subscribe<DeadLetterQueIntegrationEvent, GlobalErrorHandling>();


for (int i = 1; i <= 1; i++)
{
    await _bus.Publish(new TestQueIntegrationEvent
    {
        Id = i
    });
}

Console.ReadKey();