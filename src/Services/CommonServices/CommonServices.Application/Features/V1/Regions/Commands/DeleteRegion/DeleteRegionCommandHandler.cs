using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.RegionException;

namespace CommonServices.Application.Features.V1.Regions.Commands.DeleteRegion;
internal sealed class DeleteRegionCommandHandler : ICommandHandler<DeleteRegionCommand>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteRegionCommandHandler(IRepositoryBase<Region, Guid> regionRepository, ICurrentUser currentUser)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteRegionCommand request, CancellationToken cancellationToken)
    {
        var regionEntity = await _regionRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new RegionNotFoundException(request.Id);

        // Soft delete: the row is kept with Status = Deleted, so countries still pointing to it stay valid.
        regionEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
