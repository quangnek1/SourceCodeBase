using Microsoft.AspNetCore.Authorization;
using Shared.Common.Constants.Authorization;

namespace AerationSterilize.API.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string functionCode, string commandCode)
    {
        FunctionCode = functionCode;
        CommandCode = commandCode;
    }
    public string FunctionCode { get; }
    public string CommandCode { get; }
    public string Value => PermissionClaim.Build(FunctionCode, CommandCode);
}
