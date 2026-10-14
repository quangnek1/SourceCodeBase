using AutoMapper;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Countries.Queries.GetCountryById;
public class GetCountryByIdQueryHandler : IQueryHandler<GetCountryByIdQuery, CountryDetailDto>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly IMapper _mapper;

    public GetCountryByIdQueryHandler(IRepositoryBase<Country, Guid> countryRepository, IMapper mapper)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CountryDetailDto>> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
    {
        var country = await _countryRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CountryNotFoundException(request.Id);

        var result = _mapper.Map<CountryDetailDto>(country);

        return Result.Success(result);
    }
}
