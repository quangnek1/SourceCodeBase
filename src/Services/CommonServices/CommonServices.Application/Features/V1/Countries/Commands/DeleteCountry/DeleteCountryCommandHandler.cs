using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CountryException;

namespace CommonServices.Application.Features.V1.Countries.Commands.DeleteCountry;
internal sealed class DeleteCountryCommandHandler : ICommandHandler<DeleteCountryCommand>
{
    private readonly IRepositoryBase<Country, Guid> _countryRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteCountryCommandHandler(IRepositoryBase<Country, Guid> countryRepository, ICurrentUser currentUser)
    {
        _countryRepository = countryRepository ?? throw new ArgumentNullException(nameof(countryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteCountryCommand request, CancellationToken cancellationToken)
    {
        var countryEntity = await _countryRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CountryNotFoundException(request.Id);

        // Soft delete: the row is kept with Status = Deleted. Its cities are then hidden as well (City.Country is required).
        countryEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
