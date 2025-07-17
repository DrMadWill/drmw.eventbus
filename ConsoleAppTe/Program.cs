// See https://aka.ms/new-console-template for more information

using ConsoleAppTe;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello, World!");
IServiceCollection serviceCollection =  new ServiceCollection();

serviceCollection.AddMassTransit(config =>
{
    config.AddConsumer<TestQueHandler>();

    config.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("order_created_queue", e =>
        {
            e.ConfigureConsumer<TestQueHandler>(context);
        });
    });
});

var app = serviceCollection.BuildServiceProvider();
var busControl = app.GetRequiredService<IBusControl>();
await busControl.StartAsync();

try
{
    var bus = app.GetRequiredService<IBus>();
    await bus.Publish(new TestQue { Id = 1 });

    Console.ReadKey();
}
finally
{
    await busControl.StopAsync();
}

Console.ReadKey();






