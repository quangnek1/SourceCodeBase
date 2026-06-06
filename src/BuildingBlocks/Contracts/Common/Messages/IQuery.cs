using Contracts.Responses;
using MediatR;

namespace Contracts.Common.Messages;
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
