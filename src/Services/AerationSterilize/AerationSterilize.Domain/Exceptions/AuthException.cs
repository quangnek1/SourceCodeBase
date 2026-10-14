using Contracts.Exceptions;

namespace AerationSterilize.Domain.Exceptions;
public static class AuthException
{
    public class InvalidCredentialsException : BadRequestException
    {
        public InvalidCredentialsException()
            : base("Invalid username or password.") { }
    }

    public class InvalidRefreshTokenException : BadRequestException
    {
        public InvalidRefreshTokenException()
            : base("Invalid refresh token.") { }
    }

    public class RegisterFailedException : BadRequestException
    {
        public RegisterFailedException(string errors)
            : base(errors) { }
    }
}
