namespace Contracts.Exceptions;
public sealed class ValidationException : BadRequestException
{
    public ValidationException( IReadOnlyCollection<ValidationError> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }

    public IReadOnlyCollection<ValidationError> Errors { get; }
}
