using CommonServices.Application.Features.V1.Cities.Common.Caching;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using Contracts.Caching;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCityById;
public sealed record GetCityByIdQuery(Guid Id) : ICachedQuery<CityDetailDto>
{
    string ICachedQuery.CacheKey => CityCacheKeys.ById(Id);

    TimeSpan? ICachedQuery.Expiration => null;
}
