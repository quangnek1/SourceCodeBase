using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Regions.Commands.CreateRegion;
internal sealed class CreateRegionCommandHandler : ICommandHandler<CreateRegionCommand, Guid>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly ICurrentUser _currentUser;

    public CreateRegionCommandHandler(IRepositoryBase<Region, Guid> regionRepository, ICurrentUser currentUser)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateRegionCommand request, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code;

        // The code is optional; it is only checked when one is entered.
        if (code is not null && await _regionRepository.FindAll(x => x.Code == code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(Region), code));

        var regionEntity = Region.Create(code, request.Name, request.Note, request.SortOrder, _currentUser.UserId.ToString());
        regionEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name, t.Note)));

        _regionRepository.Add(regionEntity);

        return Result.Success(regionEntity.Id);
    }
}
