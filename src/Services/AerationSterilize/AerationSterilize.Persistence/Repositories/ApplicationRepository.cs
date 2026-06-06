using Contracts.Abstractions.Entities.Domains;
using Infrastructure.Common.Repositories;

namespace AerationSterilize.Persistence.Repositories;
internal class ApplicationRepository<T, K> : RepositoryBase<T, K, ApplicationDbContext> where T : EntityBase<K>
{
    public ApplicationRepository(ApplicationDbContext context) : base(context)
    {
    }
}
