using AutoMapper;
using DrMW.Repositories.Concretes.Works;

namespace WebApplication1.Db;

public class TestQueryRepositories : QueryRepositories<AppDbContext>
{
    public TestQueryRepositories(AppDbContext dbContext, IMapper mapper,IServiceProvider serviceProvider) 
        : base(dbContext, mapper, typeof(Program).Assembly,serviceProvider)
    {
    }
}