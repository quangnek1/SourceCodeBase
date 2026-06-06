using Contracts.Common.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Common;
public class UnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;
    public UnitOfWork(TContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async ValueTask DisposeAsync()
        => await _context.DisposeAsync();

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync();
}
