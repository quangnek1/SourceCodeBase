using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountries;
public class GetCountriesQueryHandler : IQueryHandler<GetCountriesQuery, PagedResult<CountryDto>>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;

    public GetCountriesQueryHandler(IRepositoryBase<Country, Guid> countryRepository)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
    }

    public async Task<Result<PagedResult<CountryDto>>> Handle(GetCountriesQuery request, CancellationToken cancellationToken)
    {
        var countriesQuery = _countryRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            countriesQuery = countriesQuery.Where(x => x.Code!.Contains(searchTerm)
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            countriesQuery = countriesQuery.Where(x => x.Status == status);

        if (request.RegionId is { } regionId)
            countriesQuery = countriesQuery.Where(x => x.RegionId == regionId);

        var sortExpression = CountryExtension.GetSortExpression(request.SortColumn);

        countriesQuery = request.SortOrder == SortOrder.Descending
            ? countriesQuery.OrderByDescending(sortExpression)
            : countriesQuery.OrderBy(sortExpression);

        var result = await countriesQuery
            .Select(CountryExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
