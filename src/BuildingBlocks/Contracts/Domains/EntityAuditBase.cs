using Contracts.Abstractions.Entities.Domains.Interfaces;

namespace Contracts.Abstractions.Entities.Domains;

public abstract class EntityAuditBase<T> : EntityBase<T>, IAuditable
{
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? LastModifiedDate { get; set; }

}

