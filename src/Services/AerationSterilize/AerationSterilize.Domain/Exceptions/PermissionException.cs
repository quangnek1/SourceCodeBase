using Contracts.Exceptions;

namespace AerationSterilize.Domain.Exceptions;
public static class PermissionException
{
    public class InvalidPermissionException : BadRequestException
    {
        public InvalidPermissionException(IEnumerable<string> invalidCodes)
            : base($"These permissions are not valid: {string.Join(", ", invalidCodes)}.") { }
    }
}
