using InCleanHome.ReviewsService.Domain.Model.Aggregates;
using InCleanHome.ReviewsService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.ReviewsService.Infrastructure.Persistence;

public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly ReviewsDbContext Context;
    protected BaseRepository(ReviewsDbContext context) { Context = context; }

    public async Task AddAsync(TEntity entity)         => await Context.Set<TEntity>().AddAsync(entity);
    public async Task<TEntity?> FindByIdAsync(int id)  => await Context.Set<TEntity>().FindAsync(id);
    public void Update(TEntity entity)                 => Context.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity)                 => Context.Set<TEntity>().Remove(entity);
    public async Task<IEnumerable<TEntity>> ListAsync()=> await Context.Set<TEntity>().ToListAsync();
}

public class UnitOfWork(ReviewsDbContext context) : IUnitOfWork
{
    public async Task CompleteAsync() => await context.SaveChangesAsync();
}
