namespace Shared.Common.Constants.Authorization;
public static class FunctionCode
{
    public const string DASHBOARD = "DASHBOARD";
    public const string SYSTEM = "SYSTEM";
    public const string USER = "USER";
    public const string ROLE = "ROLE";
    public const string PRODUCT = "PRODUCT";

    // Master data (CommonServices)
    public const string MASTER_DATA = "MASTER_DATA";
    public const string LANGUAGE = "LANGUAGE";
    public const string REGION = "REGION";
    public const string CURRENCY = "CURRENCY";
    public const string COUNTRY = "COUNTRY";
    public const string CITY = "CITY";
    public const string BANK = "BANK";
}

public static class CommandCode
{
    public const string VIEW = "VIEW";
    public const string CREATE = "CREATE";
    public const string UPDATE = "UPDATE";
    public const string DELETE = "DELETE";
    public const string ACTIVATE = "ACTIVATE";
    public const string INACTIVATE = "INACTIVATE";
}

public static class PermissionClaim
{
    public const string Type = "permission";
    public const string LevelType = "permission_level";

    // "PRODUCT" + "VIEW" => "PRODUCT.VIEW"
    public static string Build(string functionId, string actionId) => $"{functionId}.{actionId}";
}

public static class UserClaim
{
    public const string DepartmentId = "department_id";
}
