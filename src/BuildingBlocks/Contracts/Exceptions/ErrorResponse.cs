namespace Contracts.Exceptions;
public sealed class ErrorResponse
{
    public string Title { get; init; } = default!;

    public int Status { get; init; }

    public string Detail { get; init; } = default!;

    public IReadOnlyCollection<ValidationError>? Errors { get; init; }
}
