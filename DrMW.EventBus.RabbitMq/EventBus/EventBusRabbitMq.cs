using System.Text;
using System.Text.RegularExpressions;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Configurations;
using DrMW.EventBus.RabbitMq.Models;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DrMW.EventBus.RabbitMq.EventBus;

public class EventBusRabbitMq : BaseEventBus
{
    private readonly PersistentConnection _persistentConnection;
    private readonly IChannel _consumerChannel;
    private readonly BusConfig _busConfig;
    public EventBusRabbitMq(BusConfig config, IServiceProvider serviceProvider,ConnectionFactory? connectionFactory = null) : 
        base(config, serviceProvider)
    {
        _busConfig = config;
         connectionFactory ??= new ConnectionFactory{ Uri = new Uri(config.ConnectionUrl) };
        _persistentConnection = PersistentConnection.Instance(connectionFactory,config.ConnectionRetryCount); // Singleton
        _consumerChannel = CreateConsumerChannel().GetAwaiter().GetResult();
        SubManager.OnEventRemoved += SubManger_OnEventRemoved;
        
    }

    private void SubManger_OnEventRemoved(object? sender, string eventName)
    {
        eventName = ProcessEventName(eventName);
        TryConnect().GetAwaiter().GetResult();
        _consumerChannel.QueueUnbindAsync(queue:eventName,exchange:_busConfig.DefaultTopicName,routingKey:eventName).GetAwaiter().GetResult();
        if (SubManager.IsEmpty)
        {
            _consumerChannel.CloseAsync().GetAwaiter().GetResult();
        }
    }
    
    private async Task BasicPublishAsync(string message, string eventName)
    {
        await TryConnect();
        
        var body = Encoding.UTF8.GetBytes(message);
        var address = new PublicationAddress(ExchangeType.Direct, _busConfig.DefaultTopicName, eventName);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await _consumerChannel.BasicPublishAsync(addr: address,
            basicProperties: properties
            , body: body);

        Console.WriteLine($" Event: {eventName} Published to RabbitMQ | Message: {message}");
    }

    public override async Task Publish(IntegrationEvent @event)
    {
        var eventName = @event.GetType().Name;
        eventName = ProcessEventName(eventName);
        var message = SerializeObject(@event);
        await BasicPublishAsync(message, eventName);
    }

    public override async Task Subscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);
        Console.WriteLine("Rabbit MQ ==>>> : {0} event listening... ",eventName); 
        if (!SubManager.HasSubscriptionForEvent(eventName))
        {
            if (!_persistentConnection.IsConnection)
                await _persistentConnection.TryConnect();
        }
        SubManager.AddSubscription<T,TH>();  
        await StartBasicConsume(eventName);
    }

    private async Task StartBasicConsume(string eventName)
    {
        if (_consumerChannel != null)
        {
            await _consumerChannel.QueueDeclareAsync(queue: GetSubName(eventName),
                durable: true, exclusive: false, autoDelete: false, arguments: null);
            
            
            await _consumerChannel.QueueBindAsync(queue: GetSubName(eventName),exchange: _busConfig.DefaultTopicName
                ,routingKey: eventName);
            
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.Received += Consumer_Received;

           await _consumerChannel.BasicConsumeAsync(queue: GetSubName(eventName), autoAck: false, consumer: consumer);
        }
    }
    
    private async Task Consumer_Received(object? sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        eventName = ProcessEventName(eventName);
        var message = Encoding.UTF8.GetString(e.Body.Span);
    
        try
        {
            // process event
            await ProcessEvent(eventName, message);
            // If success work then ack
            await _consumerChannel.BasicAckAsync(e.DeliveryTag, false);
        }
        catch (Exception exception)
        {
            Console.WriteLine($" [Event Error] ::: =>>> : {eventName} Event can't process: {exception}");
            if (string.Equals(eventName, "DeadLetterQue", StringComparison.CurrentCultureIgnoreCase)) throw;
            if(!eventName.Contains("EventError")) await BasicPublishAsync(message,"EventError" + eventName);
            
            await BasicPublishAsync(SerializeObject(new DeadLetterQue
            {
                Base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(message)),
                EventName = eventName,
                AppName = _busConfig.SubscriberClientAppName,
                Prefix = _busConfig.EventNamePrefix
            }), "DeadLetterQue");
            throw;
        }
    }

    
    public override Task UnSubscribe<T, TH>()
    {
        SubManager.RemoveSubscription<T,TH>();
        return Task.CompletedTask;
    }

    private async  Task<IChannel> CreateConsumerChannel()
    {
        await TryConnect();
        var channel = await _persistentConnection.CreateChanel();
        await channel.ExchangeDeclareAsync(exchange: _busConfig.DefaultTopicName,type :"direct");
        return channel;
    }
    
    private async Task TryConnect()
    {
        if (!_persistentConnection.IsConnection)
        {
            await _persistentConnection.TryConnect();
        } 
    }

    private static string SerializeObject(object value)
    {
        return Regex.Unescape(JsonConvert.SerializeObject(value, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        })); 
    }
    
}