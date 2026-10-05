using Microsoft.AspNetCore.Authorization;

namespace AerationSterilize.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public PermissionAttribute(string functionCode, string commandCode)
    {
        FunctionCode = functionCode;
        CommandCode = commandCode;
    }
    public string FunctionCode { get; }
    public string CommandCode { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(FunctionCode, CommandCode);
    }
}
