using Contracts.Abstractions.Entities.Domains;
using Contracts.Domains.Interfaces;

namespace AerationSterilize.Domain.Entities;
public class Product : EntityAuditBase<Guid>, IDataOwned
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Description { get; private set; }
    public Guid? CreatedBy { get; set; } = Guid.NewGuid();
    public Guid? DepartmentId { get; set; } = Guid.NewGuid();

    private Product() { }

    public Product(Guid id, string name, decimal price, string description)
    {
        Id = id;
        Name = name;
        Price = price;
        Description = description;

    }
}
