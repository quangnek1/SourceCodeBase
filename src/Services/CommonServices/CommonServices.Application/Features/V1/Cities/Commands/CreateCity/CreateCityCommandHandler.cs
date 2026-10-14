using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Cities.Commands.CreateCity;
internal sealed class CreateCityCommandHandler : ICommandHandler<CreateCityCommand, Guid>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly ICurrentUser _currentUser;

    public CreateCityCommandHandler(IRepositoryBase<City, Guid> cityRepository,
        IRepositoryBase<Country, Guid> countryRepository,
        ICurrentUser currentUser)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateCityCommand request, CancellationToken cancellationToken)
    {
        if (await _cityRepository.FindAll(x => x.Code == request.Code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(City), request.Code));

        if (!await _countryRepository.FindAll(x => x.Id == request.CountryId).AnyAsync(cancellationToken))
            throw new CountryNotFoundException(request.CountryId);

        var cityEntity = City.Create(request.Code, request.Name, request.CountryId, _currentUser.UserId.ToString());
        cityEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        _cityRepository.Add(cityEntity);

        return Result.Success(cityEntity.Id);
    }
}
