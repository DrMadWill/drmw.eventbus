using Dr.EventBus.MassTransit.EventBus;
using Dr.EventBus.MassTransit.Models;
using Dr.EventBus.MassTransit.Observers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dr.EventBus.MassTransit;

public static class ServiceRegistration
{
    public static IServiceCollection AddMassTransitEventBus<AppDb>(
        this IServiceCollection services,
        Action<IBusRegistrationConfigurator> configureConsumers,
        string rabbitMqHost,
        string rabbitMqUser,
        string rabbitMqPass,
        Action<IEntityFrameworkOutboxConfigurator> configureOutbox,
        Dictionary<string,Func<string,Task>>? configureEndpoints = null)
    where AppDb : DbContext
    {
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<ConnectionObserver>();
        services.AddMassTransit(configurator =>
        {
            // Consumer-ləri qeyd edirik
            configureConsumers(configurator);

            configurator.SetKebabCaseEndpointNameFormatter();

            configurator.UsingRabbitMq((context, cfg) =>
            {
                if (configureEndpoints != null)
                {
                    // Configure Endpoints
                    foreach (var endpoint in configureEndpoints)
                    {
                        cfg.ReceiveEndpoint(endpoint.Key.GenerateRepairEventName(), e =>
                        {
                            e.Handler<RepairId>(async ctx =>
                            {
                                await endpoint.Value(ctx.Message.Id);
                            });
                        });
                    }
                }
                
                
                cfg.Host(new Uri(rabbitMqHost), h =>
                {
                    h.Username(rabbitMqUser);
                    h.Password(rabbitMqPass);
                });

                // Retry Policy
                cfg.UseMessageRetry(r =>
                {
                    r.Intervals(
                        TimeSpan.FromSeconds(3),
                        TimeSpan.FromSeconds(9),
                        TimeSpan.FromSeconds(15)
                    );
                });

                // Configure Logging
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConfigureEndpoints(context);

               
            });
            
            // Configure Outbox
            configurator.AddEntityFrameworkOutbox<AppDb>(o =>
            {
                configureOutbox(o);
                o.UseBusOutbox();
            });

            // Connection Observer Logging
            configurator.AddBusObserver<ConnectionObserver>();
        });

        services.AddScoped<IEventBus, EventBusMassTransit>();
        
        return services;
    }
}