using AutoMapper;
using DrMW.Repositories.Concretes.Works;

namespace WebApplication1.Db;

public class TestUnitOfWork : UnitOfWork<AppDbContext>
{
    public TestUnitOfWork(AppDbContext context, IMapper mapper, IServiceProvider serviceProvider) : 
        base(context, typeof(AppDbContext).Assembly, mapper, serviceProvider)
    {
    }
}
