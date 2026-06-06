using Contracts.Responses;
using MediatR;

namespace Contracts.Common.Messages;
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
