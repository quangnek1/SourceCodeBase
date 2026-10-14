using Contracts.Exceptions;

namespace AerationSterilize.Domain.Exceptions;
public static class RoleException
{
    public class RoleNotFoundException : NotFoundException
    {
        public RoleNotFoundException(Guid roleId)
            : base($"The role with the id {roleId} was not found.") { }
    }

    public class RoleAlreadyExistsException : BadRequestException
    {
        public RoleAlreadyExistsException(string name)
            : base($"The role '{name}' already exists.") { }
    }

    public class RoleInUseException : BadRequestException
    {
        public RoleInUseException(string name)
            : base($"The role '{name}' is still assigned to users.") { }
    }

    public class RoleOperationFailedException : BadRequestException
    {
        public RoleOperationFailedException(string errors)
            : base(errors) { }
    }
}
