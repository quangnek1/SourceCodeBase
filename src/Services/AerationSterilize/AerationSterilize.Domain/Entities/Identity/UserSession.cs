using Contracts.Abstractions.Entities.Domains;

namespace AerationSterilize.Domain.Entities.Identity;
public class UserSession : EntityBase<int>
{
    public Guid UserId { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiredAt { get; set; }
    public bool IsRevoked { get; set; }
    public AppUser User { get; set; } = null!;
}
