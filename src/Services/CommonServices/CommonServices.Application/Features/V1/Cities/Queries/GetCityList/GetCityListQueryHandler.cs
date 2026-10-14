using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCityList;
public class GetCityListQueryHandler : IQueryHandler<GetCityListQuery, IReadOnlyList<CityDto>>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;

    public GetCityListQueryHandler(IRepositoryBase<City, Guid> cityRepository)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
    }

    public async Task<Result<IReadOnlyList<CityDto>>> Handle(GetCityListQuery request, CancellationToken cancellationToken)
    {
        var citiesQuery = _cityRepository.FindAll();

        if (request.Status is { } status)
            citiesQuery = citiesQuery.Where(x => x.Status == status);

        if (request.CountryIds is { Count: > 0 } countryIds)
            citiesQuery = citiesQuery.Where(x => countryIds.Contains(x.CountryId));

        var result = await citiesQuery
            .OrderBy(x => x.Name)
            .Select(CityExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CityDto>>(result);
    }
}
