namespace Contracts.Identity;
public interface ITokenRevocationService
{
    Task RevokeAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
