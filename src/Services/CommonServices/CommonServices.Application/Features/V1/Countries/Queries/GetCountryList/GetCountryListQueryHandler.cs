using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountryList;
public class GetCountryListQueryHandler : IQueryHandler<GetCountryListQuery, IReadOnlyList<CountryDto>>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;

    public GetCountryListQueryHandler(IRepositoryBase<Country, Guid> countryRepository)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
    }

    public async Task<Result<IReadOnlyList<CountryDto>>> Handle(GetCountryListQuery request, CancellationToken cancellationToken)
    {
        var countriesQuery = _countryRepository.FindAll();

        if (request.Status is { } status)
            countriesQuery = countriesQuery.Where(x => x.Status == status);

        if (request.RegionIds is { Count: > 0 } regionIds)
            countriesQuery = countriesQuery.Where(x => x.RegionId != null && regionIds.Contains(x.RegionId.Value));

        var result = await countriesQuery
            .OrderBy(x => x.Name)
            .Select(CountryExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CountryDto>>(result);
    }
}
