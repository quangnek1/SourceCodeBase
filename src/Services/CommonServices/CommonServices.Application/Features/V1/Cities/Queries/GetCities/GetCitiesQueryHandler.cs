using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCities;
public class GetCitiesQueryHandler : IQueryHandler<GetCitiesQuery, PagedResult<CityDto>>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;

    public GetCitiesQueryHandler(IRepositoryBase<City, Guid> cityRepository)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
    }

    public async Task<Result<PagedResult<CityDto>>> Handle(GetCitiesQuery request, CancellationToken cancellationToken)
    {
        var citiesQuery = _cityRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            citiesQuery = citiesQuery.Where(x => x.Code!.Contains(searchTerm)
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            citiesQuery = citiesQuery.Where(x => x.Status == status);

        if (request.CountryId is { } countryId)
            citiesQuery = citiesQuery.Where(x => x.CountryId == countryId);

        var sortExpression = CityExtension.GetSortExpression(request.SortColumn);

        citiesQuery = request.SortOrder == SortOrder.Descending
            ? citiesQuery.OrderByDescending(sortExpression)
            : citiesQuery.OrderBy(sortExpression);

        var result = await citiesQuery
            .Select(CityExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
