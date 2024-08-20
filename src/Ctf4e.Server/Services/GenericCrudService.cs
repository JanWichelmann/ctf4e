using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services;

/// <summary>
/// Helper class providing generic CRUD implementations, to reduce code duplication.
/// </summary>
public class GenericCrudService<TDbContext>(TDbContext dbContext, IMapper mapper) where TDbContext : DbContext
{
    public IAsyncEnumerable<TModel> GetAllAsync<TModel, TEntity>() where TEntity : class
    {
        return dbContext.Set<TEntity>()
            .AsNoTracking()
            .ProjectTo<TModel>(mapper.ConfigurationProvider)
            .ToAsyncEnumerable();
    }

    public async Task<bool> AnyAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class
    {
        return await dbContext.Set<TEntity>().AnyAsync(cancellationToken);
    }

    public async Task<TModel> FindAsync<TModel, TEntity>(Expression<Func<TEntity, bool>> matcher, CancellationToken cancellationToken = default) where TEntity : class
    {
        var entity = await dbContext.Set<TEntity>().AsNoTracking()
            .Where(matcher)
            .FirstOrDefaultAsync(cancellationToken);
        return mapper.Map<TModel>(entity);
    }
    
    public Task<TEntity> FindAsync<TEntity>(Expression<Func<TEntity, bool>> matcher, CancellationToken cancellationToken = default) where TEntity : class
    {
        return dbContext.Set<TEntity>().AsNoTracking()
            .Where(matcher)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TModel> CreateAsync<TModel, TEntity>(TModel model, CancellationToken cancellationToken = default) where TEntity : class
    {
        var entity = mapper.Map<TEntity>(model);
        dbContext.Set<TEntity>().Add(entity);

        await dbContext.SaveChangesAsync(cancellationToken);
        return mapper.Map<TModel>(entity);
    }

    public async Task UpdateAsync<TModel, TEntity>(TModel model, CancellationToken cancellationToken = default) where TEntity : class
    {
        var entity = mapper.Map<TEntity>(model);
        dbContext.Set<TEntity>().Update(entity);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync<TEntity>(object[] key, CancellationToken cancellationToken = default) where TEntity : class
    {
        var entity = await dbContext.Set<TEntity>().FindAsync(key, cancellationToken);
        if(entity == null)
            throw new ArgumentException("Could not find entity with the specified key.", nameof(key));

        dbContext.Set<TEntity>().Remove(entity);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}