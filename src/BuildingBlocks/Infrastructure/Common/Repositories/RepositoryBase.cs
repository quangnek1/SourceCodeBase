using System.Linq.Expressions;
using Contracts.Abstractions.Entities.Domains;
using Contracts.Common.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Common.Repositories;
public class RepositoryBase<T, K, TContext> : IRepositoryBase<T, K>
    where T : EntityBase<K>
    where TContext : DbContext
{
    private readonly TContext _context;
    public RepositoryBase(TContext context)
        => _context = context;

    public IQueryable<T> FindAll(Expression<Func<T, bool>>? predicate = null,
        bool tracking = false, // Importance Always include AsNoTracking for Query Side
        params Expression<Func<T, object>>[] includeProperties)
    {
        IQueryable<T> items = tracking
        ? _context.Set<T>()
        : _context.Set<T>().AsNoTracking();

        foreach (var includeProperty in includeProperties)
            items = items.Include(includeProperty);

        if (predicate is not null)
            items = items.Where(predicate);

        return items;
    }

    public async Task<T> FindByIdAsync(K id, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includeProperties)
     => await FindAll(null, tracking: true, includeProperties)
        .SingleOrDefaultAsync(x => x.Id.Equals(id), cancellationToken);

    public async Task<T> FindSingleAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default, params Expression<Func<T, object>>[] includeProperties)
      => await FindAll(predicate, tracking: true, includeProperties)
        .SingleOrDefaultAsync(cancellationToken);

    public void Add(T entity)
        => _context.Set<T>().Add(entity);

    public void AddList(IEnumerable<T> entities)
       => _context.Set<T>().AddRange(entities);

    public void Remove(T entity)
        => _context.Set<T>().Remove(entity);
    public void RemoveMultiple(IEnumerable<T> entities)
        => _context.Set<T>().RemoveRange(entities);

    public void Update(T entity)
        => _context.Set<T>().Update(entity);


}
