using System.Net.Sockets;
using System.Text;
using DrMW.EventBus.Core.BaseModels;
using DrMW.EventBus.RabbitMq.Events;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace DrMW.EventBus.RabbitMq;

public class EventBusRabbitMq : BaseEventBus
{
    private readonly RabbitMqPersistentConnection _persistentConnection;
    private readonly IModel _consumerChannel;
    private bool _isLog = false;
    public EventBusRabbitMq(EventBusConfig config, IServiceProvider serviceProvider, IConnectionFactory connectionFactory,bool isLog = false) : base(config, serviceProvider,isLog)
    {
        _isLog = isLog;
        _persistentConnection = RabbitMqPersistentConnection.Instance(connectionFactory,config.ConnectionRetryCount,isLog); // Singleton
        _consumerChannel = CreateConsumerChannel();
        SubManager.OnEventRemoved += Submanger_OnEventRemoved;
    }

    private void Submanger_OnEventRemoved(object? sender, string eventName)
    {
        Log(nameof(Submanger_OnEventRemoved) ,"Started..");
        eventName = ProcessEventName(eventName);
        TryConnect();
        Log(nameof(Submanger_OnEventRemoved) + $"{eventName} remove stared..");
        _consumerChannel.QueueUnbind(queue:eventName,exchange:EventBusConfig.DefaultTopicName,routingKey:eventName);
        Log(nameof(Submanger_OnEventRemoved) + $"{eventName} remove finished..");
        if (SubManager.IsEmpty)
        {
            Log(nameof(Submanger_OnEventRemoved) + $"{eventName} _consumerChannel closing started..");
            _consumerChannel.Close();
            Log(nameof(Submanger_OnEventRemoved) + $"{eventName} _consumerChannel closed..");

        }

    }

    public override void Publish(IntegrationEvent @event)
    {
        Log(nameof(Publish),$" stared..");
        TryConnect();
        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(EventBusConfig.ConnectionRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    Console.WriteLine("ex => Event Bus Publish In RabbitMQ =>  " + ex);
                });
        var eventName = @event.GetType().Name;
        eventName = ProcessEventName(eventName);
        Log(nameof(Publish),$"{eventName} publishing stared..");
        
        _consumerChannel.ExchangeDeclare(exchange:EventBusConfig.DefaultTopicName,type:"direct");
       
        
        
        var message = JsonConvert.SerializeObject(@event);
        var body = Encoding.UTF8.GetBytes(message);
        policy.Execute(() =>
        {
            Log(nameof(Publish),$"Execute publishing stared..");
            var props = _consumerChannel.CreateBasicProperties();
            props.DeliveryMode = 2;
            
            _consumerChannel.QueueDeclare(queue: GetSubName(eventName),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            
            _consumerChannel.QueueBind(queue: GetSubName(eventName), exchange: EventBusConfig.DefaultTopicName,
                routingKey: eventName);
            
            _consumerChannel.BasicPublish(exchange: EventBusConfig.DefaultTopicName, routingKey: eventName,
                mandatory: true, basicProperties: props, body: body);
            Log(nameof(Publish),$"Execute publishing ended..");
        });
    }

    public override void Subscribe<T, TH>()
    {
        
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);
        Console.WriteLine("Rabbit MQ ==>>> : {0} event listening... ",eventName); 
        if (!SubManager.HasSubscriptionForEvent(eventName))
        {
            if (!_persistentConnection.IsConnection)
            {
                _persistentConnection.TryConnect();
            }

            _consumerChannel.QueueDeclare(queue: GetSubName(eventName),// ensure queue with consuming
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _consumerChannel.QueueBind(queue: GetSubName(eventName), exchange: EventBusConfig.DefaultTopicName,
                routingKey: eventName);
            
        }
        SubManager.AddSubscription<T,TH>();  
        StartBasicConsume(eventName);
    }

    private void StartBasicConsume(string eventName)
    {
        if (_consumerChannel != null)
        {
            var consumer = new EventingBasicConsumer(_consumerChannel);
            consumer.Received += Consumer_Received;

            _consumerChannel.BasicConsume(queue: GetSubName(eventName), autoAck: false, consumer: consumer);
        }

    }

    private async  void Consumer_Received(object? sender, BasicDeliverEventArgs e)
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
            Log($"{exception} occur",eventName,message);
            
        } 
        _consumerChannel.BasicAck(e.DeliveryTag,multiple:false);
    }

    public override void UnSubscribe<T, TH>()
    {
        SubManager.RemoveSubscription<T,TH>();
       
    }

    private IModel CreateConsumerChannel()
    {
        Log(nameof(CreateConsumerChannel),$" stared..");
        TryConnect();
        Log(nameof(CreateConsumerChannel),$" CreateModel stared..");
        var channel = _persistentConnection.CreateModel();
        Log(nameof(CreateConsumerChannel),$" DefaultTopicName adding..");
        channel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName,type :"direct");
        Log(nameof(CreateConsumerChannel),$" ended..");
        return channel;
    }
    
    private  void TryConnect()
    {
        Log(nameof(TryConnect),$" stared..");
        if (!_persistentConnection.IsConnection)
        {
            Log(nameof(TryConnect),$" _persistentConnection TryConnect stared..");
            _persistentConnection.TryConnect();
            Log(nameof(TryConnect),$" _persistentConnection TryConnect ended..");
        } 
        Log(nameof(TryConnect),$" ended..");
    }
    
    private void Log(params string[] logs)
    {
        if (_isLog)
        {
            Console.WriteLine("EventBusRabbitMq >>>>>>>>>>>>> : " + string.Join(" | ", logs));
        }
    }
    
    

}