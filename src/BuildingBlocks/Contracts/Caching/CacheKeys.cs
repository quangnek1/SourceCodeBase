namespace Contracts.Caching;

// Keys shared by every service. Service-specific keys live in the service itself.
public static class CacheKeys
{
    public const string PermissionPrefix = "perm:";

    public static string Permissions(Guid userId) => $"{PermissionPrefix}{userId}";

    public static string RevokedToken(string jti) => $"revoked:jti:{jti}";
}
