using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.CityException;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Cities.Commands.UpdateCity;
internal sealed class UpdateCityCommandHandler : ICommandHandler<UpdateCityCommand, CityDetailDto>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateCityCommandHandler(IRepositoryBase<City, Guid> cityRepository,
        IRepositoryBase<Country, Guid> countryRepository,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CityDetailDto>> Handle(UpdateCityCommand request, CancellationToken cancellationToken)
    {
        var cityEntity = await _cityRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CityNotFoundException(request.Id);

        if (await _cityRepository.FindAll(x => x.Code == request.Code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<CityDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(City), request.Code));

        if (!await _countryRepository.FindAll(x => x.Id == request.CountryId).AnyAsync(cancellationToken))
            throw new CountryNotFoundException(request.CountryId);

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        cityEntity.Update(request.Code, request.Name, request.CountryId, _currentUser.UserId.ToString());
        cityEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        var result = _mapper.Map<CityDetailDto>(cityEntity);

        return Result.Success(result);
    }
}
