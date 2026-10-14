using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CityException;

namespace CommonServices.Application.Features.V1.Cities.Commands.SetCityStatus;
internal sealed class SetCityStatusCommandHandler : ICommandHandler<SetCityStatusCommand>
{
    private readonly IRepositoryBase<City, Guid> _cityRepository;
    private readonly ICurrentUser _currentUser;

    public SetCityStatusCommandHandler(IRepositoryBase<City, Guid> cityRepository, ICurrentUser currentUser)
    {
        _cityRepository = cityRepository ?? throw new ArgumentNullException(nameof(cityRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetCityStatusCommand request, CancellationToken cancellationToken)
    {
        var cityEntity = await _cityRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CityNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
            cityEntity.Activate(_currentUser.UserId.ToString());
        else
            cityEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
