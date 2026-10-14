using AutoMapper;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CityException;

namespace CommonServices.Application.Features.V1.Cities.Queries.GetCityById;
public class GetCityByIdQueryHandler : IQueryHandler<GetCityByIdQuery, CityDetailDto>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;
    private readonly IMapper _mapper;

    public GetCityByIdQueryHandler(IRepositoryBase<City, Guid> cityRepository, IMapper mapper)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CityDetailDto>> Handle(GetCityByIdQuery request, CancellationToken cancellationToken)
    {
        var city = await _cityRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CityNotFoundException(request.Id);

        var result = _mapper.Map<CityDetailDto>(city);

        return Result.Success(result);
    }
}
