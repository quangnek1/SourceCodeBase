using Contracts.Responses;
using Contracts.Responses.Interfaces;
public class ValidationResult<TValue> : Result<TValue>, IValidationResult
{
    protected internal ValidationResult(Error[] error) : base(default, false, IValidationResult.ValidationError)
    {
        Errors = error;
    }

    public Error[] Errors { get; }

    public static ValidationResult<TValue> WithError(Error[] errors) => new(errors);
}
