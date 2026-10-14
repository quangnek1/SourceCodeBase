using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.RegionException;

namespace CommonServices.Application.Features.V1.Regions.Commands.SetRegionStatus;
internal sealed class SetRegionStatusCommandHandler : ICommandHandler<SetRegionStatusCommand>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly ICurrentUser _currentUser;

    public SetRegionStatusCommandHandler(IRepositoryBase<Region, Guid> regionRepository, ICurrentUser currentUser)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetRegionStatusCommand request, CancellationToken cancellationToken)
    {
        var regionEntity = await _regionRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new RegionNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
            regionEntity.Activate(_currentUser.UserId.ToString());
        else
            regionEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
