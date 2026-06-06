namespace Contracts.Abstractions.Entities.Domains.Interfaces;

public interface IEntityBase<T>
{
    T Id { get; set; }
}
