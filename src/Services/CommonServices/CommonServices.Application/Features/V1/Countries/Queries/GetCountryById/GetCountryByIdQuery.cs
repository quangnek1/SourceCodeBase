using CommonServices.Application.Features.V1.Countries.Common.Caching;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountryById;
public sealed record GetCountryByIdQuery(Guid Id) : ICachedQuery<CountryDetailDto>
{
    string ICachedQuery.CacheKey => CountryCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
