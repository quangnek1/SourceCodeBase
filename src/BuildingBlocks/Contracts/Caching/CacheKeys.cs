namespace Contracts.Caching;

// Keys shared by every service. Service-specific keys live in the service itself.
public static class CacheKeys
{
    public static string Permissions(Guid userId) => $"perm:{userId}";

    public static string RevokedToken(string jti) => $"revoked:jti:{jti}";
}
