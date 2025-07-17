using DrMW.EventBus.Core.Abstractions;
using DrMW.EventBus.RabbitMq.Configurations;
using DrMW.EventBus.RabbitMq.EventBus;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace DrMW.EventBus.RabbitMq;

public static class ServiceRegistration
{
    public static  IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,string appName,string connectionString)
    {
        serviceCollection.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(new BusConfig
        {
            ConnectionRetryCount = 5,
            SubscriberClientAppName = appName,
            ConnectionUrl = connectionString
        }, sp));
        
        return serviceCollection;
    }
    
    
    // public static  IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,string appName,string connectionString,ILogging)
    // {
    //     serviceCollection.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(new BusConfig
    //     {
    //         ConnectionRetryCount = 5,
    //         SubscriberClientAppName = appName,
    //         ConnectionUrl = connectionString
    //     }, sp));
    //     
    //     return serviceCollection;
    // }
    
    //
    
    
    public static  IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,string appName,string connectionString,ConnectionFactory connectionFactory)
    {
        serviceCollection.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(new BusConfig
        {
            ConnectionRetryCount = 5,
            SubscriberClientAppName = appName,
            ConnectionUrl = connectionString
        }, sp,connectionFactory));
        return serviceCollection;
    }
    
    
    public static  IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,BusConfig config)
    {
        serviceCollection.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(config, sp));
        return serviceCollection;
    }
    
    public static  IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,BusConfig config,ConnectionFactory connectionFactory)
    {
        serviceCollection.AddSingleton<IEventBus>(sp => new EventBusRabbitMq(config, sp, connectionFactory));
        return serviceCollection;
    }
}