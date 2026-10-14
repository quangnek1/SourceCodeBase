using Contracts.Responses;

namespace CommonServices.Application.Common.Errors;
public static class MasterDataErrors
{
    public static Error CodeAlreadyExists(string entityName, string code)
        => new($"{entityName.ToUpperInvariant()}_CODE_EXISTS", $"The {entityName.ToLowerInvariant()} code '{code}' already exists.");
}
