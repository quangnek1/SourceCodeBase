using CommonServices.Application.Features.V1.Banks.Common.Caching;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBankById;
public sealed record GetBankByIdQuery(Guid Id) : ICachedQuery<BankDetailDto>
{
    string ICachedQuery.CacheKey => BankCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
