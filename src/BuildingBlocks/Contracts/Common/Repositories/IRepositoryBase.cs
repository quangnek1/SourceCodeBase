using System.Linq.Expressions;

namespace Contracts.Common.Repositories;
public interface IRepositoryBase<T, in K> where T : class
{
    IQueryable<T> FindAll(Expression<Func<T, bool>>? predicate = null,
        bool tracking = false,
        params Expression<Func<T, object>>[] includeProperties);

    Task<T> FindByIdAsync(K id,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includeProperties);

    Task<T> FindSingleAsync(Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includeProperties);

    void Add(T entity);

    void Update(T entity);

    void Remove(T entity);

    void RemoveMultiple(IEnumerable<T> entities);
}
