using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Countries.Commands.SetCountryStatus;
internal sealed class SetCountryStatusCommandHandler : ICommandHandler<SetCountryStatusCommand>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly ICurrentUser _currentUser;

    public SetCountryStatusCommandHandler(IRepositoryBase<Country, Guid> countryRepository, ICurrentUser currentUser)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetCountryStatusCommand request, CancellationToken cancellationToken)
    {
        var countryEntity = await _countryRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CountryNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
            countryEntity.Activate(_currentUser.UserId.ToString());
        else
            countryEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
