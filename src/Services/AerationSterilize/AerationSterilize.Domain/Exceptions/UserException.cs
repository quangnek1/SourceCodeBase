using Contracts.Exceptions;

namespace AerationSterilize.Domain.Exceptions;
public static class UserException
{
    public class UserNotFoundException : NotFoundException
    {
        public UserNotFoundException(Guid userId)
            : base($"The user with the id {userId} was not found.") { }
    }

    public class UserOperationFailedException : BadRequestException
    {
        public UserOperationFailedException(string errors)
            : base(errors) { }
    }
}
