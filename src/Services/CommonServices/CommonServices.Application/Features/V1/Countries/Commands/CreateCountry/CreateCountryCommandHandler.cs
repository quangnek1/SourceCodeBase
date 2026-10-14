using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Countries.Common.Services;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Countries.Commands.CreateCountry;
internal sealed class CreateCountryCommandHandler : ICommandHandler<CreateCountryCommand, Guid>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;

    public CreateCountryCommandHandler(IRepositoryBase<Country, Guid> countryRepository,
        IRepositoryBase<Region, Guid> regionRepository,
        IRepositoryBase<Currency, Guid> currencyRepository,
        ICurrentUser currentUser)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateCountryCommand request, CancellationToken cancellationToken)
    {
        if (await _countryRepository.FindAll(x => x.Code == request.Code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(Country), request.Code));

        await CountryReferenceChecker.EnsureReferencesExistAsync(_regionRepository, _currencyRepository,
            request.RegionId, request.CurrencyId, cancellationToken);

        var countryEntity = Country.Create(request.Code, request.Name, request.RegionId, request.CurrencyId, _currentUser.UserId.ToString());
        countryEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        _countryRepository.Add(countryEntity);

        return Result.Success(countryEntity.Id);
    }
}
