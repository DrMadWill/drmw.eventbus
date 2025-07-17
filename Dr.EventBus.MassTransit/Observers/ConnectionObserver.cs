using MassTransit;
using Microsoft.Extensions.Logging;

namespace Dr.EventBus.MassTransit.Observers;

public class ConnectionObserver(ILogger<ConnectionObserver> logger) : IBusObserver
{
    public void PostCreate(IBus bus)
    {
        logger.LogInformation("✅ MassTransit connection successfully established.");
      
    }

    public void CreateFaulted(Exception exception)
    {
        logger.LogError(exception, "❌ MassTransit connection faulted!");
    }

    public Task PreStart(IBus bus) => Task.CompletedTask;

    public Task PostStart(IBus bus, Task<BusReady> busReady)
    {
        logger.LogInformation("🚀 MassTransit started.");
        return Task.CompletedTask;
    }

    public Task StartFaulted(IBus bus, Exception exception)
    {
        logger.LogError(exception, "🚫 MassTransit failed to start.");
        return Task.CompletedTask;
    }

    public Task PreStop(IBus bus)
    {
        logger.LogInformation("🛑 MassTransit stopping...");
        return Task.CompletedTask;
    }

    public Task PostStop(IBus bus)
    {
        logger.LogInformation("✅ MassTransit stopped.");
        return Task.CompletedTask;
    }

    public Task StopFaulted(IBus bus, Exception exception)
    {
        logger.LogError(exception, "⚠️ MassTransit stop faulted.");
        return Task.CompletedTask;
    }
}