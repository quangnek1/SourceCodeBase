using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CityException;

namespace CommonServices.Application.Features.V1.Cities.Commands.DeleteCity;
internal sealed class DeleteCityCommandHandler : ICommandHandler<DeleteCityCommand>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteCityCommandHandler(IRepositoryBase<City, Guid> cityRepository, ICurrentUser currentUser)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteCityCommand request, CancellationToken cancellationToken)
    {
        var cityEntity = await _cityRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CityNotFoundException(request.Id);

        // Soft delete: the row is kept with Status = Deleted.
        cityEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
