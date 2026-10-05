namespace Shared.Common.Constants.Authorization;
public static class FunctionCode
{
    public const string DASHBOARD = "DASHBOARD";
    public const string SYSTEM = "SYSTEM";
    public const string USER = "USER";
    public const string ROLE = "ROLE";
    public const string PRODUCT = "PRODUCT";
}

public static class CommandCode
{
    public const string VIEW = "VIEW";
    public const string CREATE = "CREATE";
    public const string UPDATE = "UPDATE";
    public const string DELETE = "DELETE";
}

public static class PermissionClaim
{
    public const string Type = "permission";

    // "PRODUCT" + "VIEW" => "PRODUCT.VIEW"
    public static string Build(string functionId, string actionId) => $"{functionId}.{actionId}";
}
