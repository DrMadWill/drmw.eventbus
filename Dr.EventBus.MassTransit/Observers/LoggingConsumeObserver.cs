using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dr.EventBus.MassTransit.Observers;

public class LoggingConsumeObserver(ILogger<LoggingConsumeObserver> logger) : IConsumeObserver
{
    private string Json(object obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });
    }

    private string GetQueueName(ConsumeContext context)
    {
        var address = context.ReceiveContext?.InputAddress?.ToString();
        return string.IsNullOrEmpty(address) ? "unknown" : new Uri(address).Segments.Last().Trim('/');
    }

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        var queueName = GetQueueName(context);
        logger.LogInformation("Listening Start → Queue: {Queue}, EventModelName: {Event}, Message: {Message}",
            queueName, typeof(T).Name, Json(context.Message));
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        var queueName = GetQueueName(context);
        logger.LogInformation("Listening End : Successful → Queue: {Queue}, EventModelName: {Event}, Message: {Message}",
            queueName, typeof(T).Name, Json(context.Message));
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        var queueName = GetQueueName(context);
        logger.LogError(exception, "Listening End : Fail → Queue: {Queue}, EventModelName: {Event}, Message: {Message}",
            queueName, typeof(T).Name, Json(context.Message));
        return Task.CompletedTask;
    }
}
