using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CommonServices.Application.Common;
using Shared.Common.Constants.Authorization;

namespace CommonServices.API.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _user;

    public CurrentUser(IHttpContextAccessor accessor)
        => _user = accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid UserId => Guid.Parse(_user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public Guid? DepartmentId =>
        Guid.TryParse(_user.FindFirstValue(UserClaim.DepartmentId), out var id) ? id : null;

    public string? TokenId => _user.FindFirstValue(JwtRegisteredClaimNames.Jti);

    public DateTimeOffset? TokenExpiresAt =>
        long.TryParse(_user.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : null;

    public AccessLevel GetAccessLevel(string functionCode, string commandCode)
    {
        var prefix = PermissionClaim.Build(functionCode, commandCode) + ":";
        var claim = _user.FindAll(PermissionClaim.LevelType)
                         .FirstOrDefault(c => c.Value.StartsWith(prefix));

        return claim is not null && int.TryParse(claim.Value[prefix.Length..], out var level)
            ? (AccessLevel)level
            : AccessLevel.None;
    }
}
