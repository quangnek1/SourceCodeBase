namespace Contracts.Abstractions.Entities.Domains.Interfaces;

public interface IDateTracking
{
    DateTimeOffset CreatedDate { get; set; }
    DateTimeOffset? LastModifiedDate { get; set; }
}
