using Contracts.Abstractions.Entities.Domains.Interfaces;

namespace Contracts.Abstractions.Entities.Domains;

public abstract class EntityBase<TKey> : IEntityBase<TKey>
{
    public TKey Id { get; set; }

}
