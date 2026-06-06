using Contracts.Responses;
using MediatR;

namespace Contracts.Common.Messages;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
