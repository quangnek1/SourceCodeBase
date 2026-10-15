using Contracts.Abstractions.Entities.Domains;
using Contracts.Domains.Interfaces;

namespace AerationSterilize.Domain.Entities;
public class Product : EntityAuditBase<Guid>, IDataOwned
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Description { get; private set; }
    // Owner of the row, used by the data scope (AccessLevel.Own / AccessLevel.Department). Set on create.
    public Guid? CreatedBy { get; set; }
    public Guid? DepartmentId { get; set; }

    private Product() { }

    public Product(Guid id, string name, decimal price, string description)
    {
        Id = id;
        Name = name;
        Price = price;
        Description = description;

    }
}
