using System.Linq.Expressions;
using AutoMapper;
using Dr.EventBus.MassTransit.Models;
using DrMW.Core.Models.Abstractions;
using DrMW.Repositories.Abstractions.Works;
using DrMW.Repositories.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dr.EventBus.MassTransit.EventBus;

public interface IEventBus
{
    Task Publish<T>(T message,bool save = false) where T : class;

    #region Repair
    
    Task RepairEvent(string id, string repairElement, bool save = false);
    
    Task RepairEvent<TEntity>(string id, bool save = false);

    Task<bool> DefaultRepair<TEntity, TPrimary>(TPrimary id,bool save = false, string repairElement = "")
        where TEntity : class, IOriginEntity<TPrimary>;

    Task<bool> DefaultRepairIntPrimary<TEntity>(int id, bool save = false, string repairElement = "")
        where TEntity : class, IOriginEntity<int>;

    Task<bool> DefaultRepairStringPrimary<TEntity>(string id, bool save = false, string repairElement = "")
        where TEntity : class, IOriginEntity<string>;

    Task<bool> DefaultRepairGuidPrimary<TEntity>(Guid id, bool save = false, string repairElement = "")
        where TEntity : class, IOriginEntity<Guid>;

    #endregion

    #region Sync

    Task SendSyc<TEvent, TEntity, TPrimary>(Expression<Func<TEntity, bool>> predicate, string logKey, bool save = false,
        params Expression<Func<TEntity, object>>[]? including)
        where TEntity : class, IBaseEntity<TPrimary>
        where TEvent : class;
    
    
    Task SendSyc<TEvent, TEntity>
    (Expression<Func<TEntity, bool>> predicate, string logKey,bool save = false,
        params Expression<Func<TEntity, object>>[]? including)
        where TEntity : class
        where TEvent : class;
    
    Task SendSyc<TEvent, TEntity, TPrimary>
    (Func<IQueryable<TEntity>, IQueryable<TEntity>> func, string logKey,
        params Expression<Func<TEntity, object>>[]? including)
        where TEntity : class, IBaseEntity<TPrimary>
        where TEvent : class;
    
    
    Task SyncData<TEvent, TEntity, TPrimary>(TEvent @event,Expression<Func<TEntity,bool>> predicate)
        where TEvent : class, IHasDelete
        where TEntity : class, IOriginEntity<TPrimary>;

    #endregion
}

public class EventBusMassTransit(IPublishEndpoint publishEndpoint,
    IUnitOfWork unitOfWork,
    ISendEndpointProvider sendEndpoint,
    IMapper mapper,
    ILogger<EventBusMassTransit> logger) : IEventBus
{
    public async Task Publish<T>(T message,bool save = false) where T : class
    {
        await publishEndpoint.Publish(message);
        if (save)
            await unitOfWork.CommitAsync();
    }

    #region Repair
    
    
    public async Task RepairEvent(string id, string repairElement,bool save = false)
    {
        var endpoint = await sendEndpoint.GetSendEndpoint(new Uri("queue:" + repairElement.GenerateRepairEventName()));
        await endpoint.Send(new RepairId(id));
        if (save)
            await unitOfWork.CommitAsync();
    }

    public async Task RepairEvent<TEntity>(string id, bool save = false)
        => await RepairEvent(id, typeof(TEntity).Name, save);


    public async Task<bool> DefaultRepair<TEntity, TPrimary>(TPrimary id,bool save = false, string repairElement = "") 
        where TEntity : class, IOriginEntity<TPrimary>
    {
        if (await unitOfWork.OriginRepository<TEntity, TPrimary>()
                .AnyAsync(s => s.Id.Equals(id)))
            return false;
        
        var repair = string.IsNullOrEmpty(repairElement) ? typeof(TEntity).Name.GenerateRepairEventName() : repairElement;
        await RepairEvent(id.ToString(), repair, save);
        return true;
    }

    public async Task<bool> DefaultRepairIntPrimary<TEntity>(int id, bool save = false, string repairElement = "")
        where TEntity : class, IOriginEntity<int>
        => await DefaultRepair<TEntity, int>(id, save, repairElement);

    public async Task<bool> DefaultRepairStringPrimary<TEntity>(string id,bool save = false, string repairElement = "") 
        where TEntity : class, IOriginEntity<string>
        => await DefaultRepair<TEntity, string>(id, save, repairElement);

    public async Task<bool> DefaultRepairGuidPrimary<TEntity>(Guid id,bool save = false ,string repairElement = "") 
        where TEntity : class, IOriginEntity<Guid>
        => await DefaultRepair<TEntity, Guid>(id, save, repairElement);

    
    #endregion

    #region Sync

    
    public async Task SendSyc<TEvent, TEntity, TPrimary>(Expression<Func<TEntity, bool>> predicate,
        string logKey,bool save = false, params Expression<Func<TEntity, object>>[]? including) 
        where TEvent : class 
        where TEntity : class, IBaseEntity<TPrimary>
    {
        try
        {
            var repo = unitOfWork.AnonymousRepository<TEntity>();
            TEntity? entity = await  (including != null ? repo.FindByIncludingQueryable(predicate,including) : repo.Queryable().Where(predicate))
                .FirstOrDefaultAsync();
            
            if (entity is null)
            {
                logger.LogError(typeof(TEvent).Name + $" not syc | data not found. logKey: {logKey} ");
                return;
            }

            var model = mapper.Map<TEvent>(entity);
            await Publish(model,save);
        }
        catch (Exception e)
        {
            logger.LogError(typeof(TEvent).Name + $" not syc | error occur . logKey:  {logKey} | Error {e}");
        }
    }

    public async Task SendSyc<TEvent, TEntity>(Expression<Func<TEntity, bool>> predicate, string logKey,bool save = false,
        params Expression<Func<TEntity, object>>[]? including) 
        where TEvent : class 
        where TEntity : class
    {
        try
        {
            var repo = unitOfWork.AnonymousRepository<TEntity>();
            TEntity? entity = await  (including != null ? repo.FindByIncludingQueryable(predicate,including) : repo.Queryable().Where(predicate)).AsNoTracking()
                .FirstOrDefaultAsync();
            
            if (entity is null)
            {
                logger.LogError(typeof(TEvent).Name + $" not syc | data not found. logKey: {logKey} ");
                return;
            }

            var model = mapper.Map<TEvent>(entity);
            await Publish(model);
        }
        catch (Exception e)
        {
            logger.LogError(typeof(TEvent).Name + $" not syc | error occur . logKey:  {logKey} | Error {e}");
        }
    }

    public async Task SendSyc<TEvent, TEntity, TPrimary>(Func<IQueryable<TEntity>, IQueryable<TEntity>> func, string logKey, 
        params Expression<Func<TEntity, object>>[]? including) 
        where TEvent : class where TEntity : class, IBaseEntity<TPrimary>
    {
        try
        {
            var repo = unitOfWork.Repository<TEntity, TPrimary>();
            var all = await func(repo.Queryable()).AsNoTracking().ToListAsync();
            
            
            if (!all.Any())
                return;
            

            var models = all.Select( mapper.Map<TEvent>).ToList();
            foreach (var model in models)
            {
                await Publish(model);
            }

            await unitOfWork.CommitAsync();
        }
        catch (Exception e)
        {
            logger.LogError(typeof(TEvent).Name + $" all not syc | error occur . logKey:  {logKey} | Error {e}");
        }
    }

    public async Task SyncData<TEvent, TEntity, TPrimary>(TEvent @event, Expression<Func<TEntity, bool>> predicate) 
        where TEvent : class, IHasDelete 
        where TEntity : class, IOriginEntity<TPrimary>
    {
        var repo = unitOfWork. OriginRepository<TEntity, TPrimary>();
        if (@event.IsDeleted == true)
        {
            var dict = await  repo.Table.FirstOrDefaultAsync(predicate);
            if (dict != null)
            {
                await repo.RemoveAsync(dict);
            }
        }
        else
        {
            if (await repo.Queryable().AsNoTracking().AnyAsync(predicate))
            {
                Console.WriteLine("Sync Service Update =>>>>> : {0} | {1} ", typeof(TEvent).Name,@event.JsonString());
                var dict = await  repo.Table.FirstOrDefaultAsync(predicate);
                mapper.Map(@event, dict); 
                await repo.UpdateAsync(dict);
            }
            else
            {
                Console.WriteLine("Sync Service Add =>>>>> : {0} | {1} ", typeof(TEvent).Name,@event.JsonString());
                var newEntity = mapper.Map<TEntity>(@event);
                
                await repo.AddAsync(newEntity);
            }
        }

        await unitOfWork.CommitAsync();
    }

    #endregion
  
}