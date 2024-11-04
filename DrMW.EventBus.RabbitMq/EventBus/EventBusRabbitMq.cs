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
        
        if (config.OnDeadLetter != null)
            SubscribeToDeadLetterQueue().GetAwaiter().GetResult();
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
        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(_busConfig.ConnectionRetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    Console.WriteLine("ex => Event Bus Publish In RabbitMQ =>  " + ex);
                });
        
        var body = Encoding.UTF8.GetBytes(message);
        var address = new PublicationAddress(ExchangeType.Direct, _busConfig.DefaultTopicName, eventName);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        policy.Execute(() =>
        {
            _consumerChannel.BasicPublishAsync(addr : address,
                basicProperties : properties
                ,body: body).GetAwaiter().GetResult();
        });

        Console.WriteLine($"Mesaj ana kuyruğa tekrar gönderildi: Event: {eventName}");
    }

    public override async Task Publish(IntegrationEvent @event)
    {
        var eventName = @event.GetType().Name;
        eventName = ProcessEventName(eventName);
        var message = JsonConvert.SerializeObject(@event);
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
                durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
                {
                    {"x-dead-letter-exchange", _busConfig.DeadLetterExchangeName}, // DLX tanımlama
                    {"x-dead-letter-routing-key", eventName + ".dlq"} // DLQ routing anahtarı tanımlama
                }!);
            
            
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
        int maxRetryCount = 5;
        // Mesaj başlığında retry count varsa al, yoksa 0 olarak başlat
        var headers = e.BasicProperties.Headers;
        int retryCount = headers != null && headers.TryGetValue("retry-count", out var header)
            ? (int)(header ?? 0)
            : 0;
    
        try
        {
            // Mesajı işle
            await ProcessEvent(eventName, message);
            await _consumerChannel.BasicAckAsync(e.DeliveryTag, false); // Başarılı işlenirse onayla
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Mesaj işlenemedi: {exception}");

            // Retry limitine ulaştıysa, mesajı DLQ'ye yönlendir
            if (retryCount >= maxRetryCount)
            {
                // Mesajı DLQ'ye yönlendirin veya başka bir log işlemi yapın
                await _consumerChannel.BasicNackAsync(e.DeliveryTag, false, false); // DLQ'ye taşı
                Console.WriteLine($"Mesaj DLQ'ye taşındı: {message}");
            }
            else
            {
                // Retry count artır ve tekrar kuyruğa koy
                var properties = new BasicProperties
                {
                    Persistent = true,
                    Headers = headers ?? new Dictionary<string, object>()!
                };
                properties.Headers["retry-count"] = retryCount + 1;
                var address = new PublicationAddress(ExchangeType.Direct, _busConfig.DefaultTopicName, eventName);
                // Mesajı tekrar kuyruğa koy
                await _consumerChannel.BasicPublishAsync(
                    addr:address,
                    basicProperties:properties,
                    body: e.Body);
            
                await _consumerChannel.BasicAckAsync(e.DeliveryTag, false); // Orijinal mesajı onayla
            }
        }
    }

    
    public async Task SubscribeToDeadLetterQueue()
    {
        // DLQ için kuyruğun adını alıyoruz.
        var dlqQueueName = _busConfig.DeadLetterQueueName;

        // DLQ kuyruğunu oluştur ve dinlemeye başla
        await _consumerChannel.QueueDeclareAsync(
            queue: dlqQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.Received += DLQConsumer_Received; // Mesajlar geldiğinde çalışacak metodu belirliyoruz.
    
        await _consumerChannel.BasicConsumeAsync(queue: dlqQueueName, autoAck: false, consumer: consumer);
    }

    private async Task DLQConsumer_Received(object? sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        var message = Encoding.UTF8.GetString(e.Body.Span);

        try
        {
            // Mesaj üzerinde özel işlemler yapabilirsiniz, örneğin loglama
            Console.WriteLine($"DLQ'dan mesaj alındı: Event: {eventName}, Mesaj: {message}");
        
            // Mesajın tekrar işlenmesi gerekiyorsa buraya ekleyebilirsiniz
            // await RequeueMessage(message, eventName);

             await _busConfig.OnDeadLetter!(eventName, message);
             
            // İşleme başarılı olursa mesajı onaylayın
            await _consumerChannel.BasicAckAsync(e.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DLQ mesaj işlenirken hata: {ex.Message}");
        
            // Başarısız işleme durumunda DLQ mesajı işaretleyin
            await _consumerChannel.BasicNackAsync(e.DeliveryTag, false, false);
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