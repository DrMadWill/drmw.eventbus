using System.Net.Sockets;
using System.Text;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Configurations;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

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

    public override async Task Publish(IntegrationEvent @event)
    {
        await TryConnect();
        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(_busConfig.ConnectionRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    Console.WriteLine("ex => Event Bus Publish In RabbitMQ =>  " + ex);
                });
        var eventName = @event.GetType().Name;
        eventName = ProcessEventName(eventName);
        var message = JsonConvert.SerializeObject(@event);
        var body = Encoding.UTF8.GetBytes(message);
        policy.Execute(() =>
        {
            _consumerChannel.BasicPublishAsync(exchange: _busConfig.DefaultTopicName, routingKey: eventName, body: body).GetAwaiter().GetResult();
        });
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
            
            await _consumerChannel.QueueBindAsync(queue: GetSubName(eventName),exchange: _busConfig.DefaultTopicName,routingKey: eventName);
            
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.Received += Consumer_Received;

           await _consumerChannel.BasicConsumeAsync(queue: GetSubName(eventName), autoAck: true, consumer: consumer);
        }
    }

    private async  Task Consumer_Received(object? sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        eventName = ProcessEventName(eventName);
        var message = Encoding.UTF8.GetString(e.Body.Span);

        try
        {
            await ProcessEvent(eventName, message);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
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
    

}