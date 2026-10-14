using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Services;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Countries.Commands.UpdateCountry;
internal sealed class UpdateCountryCommandHandler : ICommandHandler<UpdateCountryCommand, CountryDetailDto>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateCountryCommandHandler(IRepositoryBase<Country, Guid> countryRepository,
        IRepositoryBase<Region, Guid> regionRepository,
        IRepositoryBase<Currency, Guid> currencyRepository,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CountryDetailDto>> Handle(UpdateCountryCommand request, CancellationToken cancellationToken)
    {
        var countryEntity = await _countryRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CountryNotFoundException(request.Id);

        if (await _countryRepository.FindAll(x => x.Code == request.Code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<CountryDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(Country), request.Code));

        await CountryReferenceChecker.EnsureReferencesExistAsync(_regionRepository, _currencyRepository,
            request.RegionId, request.CurrencyId, cancellationToken);

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        countryEntity.Update(request.Code, request.Name, request.RegionId, request.CurrencyId, _currentUser.UserId.ToString());
        countryEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        var result = _mapper.Map<CountryDetailDto>(countryEntity);

        return Result.Success(result);
    }
}
