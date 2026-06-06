using Contracts.Abstractions.Entities.Domains;

namespace AerationSterilize.Domain.Entities;
public class Product : EntityAuditBase<Guid>
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Description { get; private set; }

    private Product() { }

    public Product(Guid id, string name, decimal price, string description)
    {
        Id = id;
        Name = name;
        Price = price;
        Description = description;
    }
}
