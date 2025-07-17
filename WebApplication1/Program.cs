using Dr.EventBus.MassTransit;
using Dr.EventBus.MassTransit.EventBus;
using Dr.EventBus.MassTransit.Models;
using DrMW.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using WebApplication1;
using WebApplication1.Db;
using WebApplication1.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseLoggerFactory(LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        })));


builder.Services.LayerRepositoriesRegister<TestUnitOfWork, TestQueryRepositories, TestServiceManager, AppDbContext, AppDbContext>();


var configureEndpoints = new Dictionary<string, Func<string, Task>>
{
    { "test-config-a", async message =>
        {
            Console.WriteLine($"Received message: {message}");
            await Task.CompletedTask;
        }
    },
    { "another-endpoint", async message =>
        {
            Console.WriteLine($"Received another message: {message}");
            await Task.CompletedTask;
        }
    }
};



builder.Services.AddMassTransitEventBus<AppDbContext>(config =>
    {
        // Register Consumers
        config.AddConsumer<TestConfiqHandler>();
    }, "rabbitmq://localhost", "guest", "guest",
    configDb =>
    {
        configDb.UseSqlServer();
    },configureEndpoints);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", async () =>
    {
        using var scope = app.Services.CreateScope();
        //var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        await using var db =  scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        
        await eventBus.Publish(new TestConfiq
        {
            Info = "TestConfiq message from WeatherForecast endpoint"
        },true);
       
        
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();


app.MapPost("/weatherforecast2", async (WeatherForecast forecast) =>
    {
        using var scope = app.Services.CreateScope();
        //var sendProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
        await using var db =  scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        // var endpoint = await sendProvider.GetSendEndpoint(new Uri("queue:another-endpoint"));
        // await endpoint.Send(new RepairId("123"));
        await eventBus.RepairEvent("123", "test-config-a",true);
        Console.WriteLine("Fixed is working");
    })
    .WithName("GetWeatherForecastByName")
    .WithOpenApi()
    .Produces<WeatherForecast>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}