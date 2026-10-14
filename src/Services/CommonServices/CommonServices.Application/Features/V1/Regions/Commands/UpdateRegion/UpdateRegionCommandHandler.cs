using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.RegionException;

namespace CommonServices.Application.Features.V1.Regions.Commands.UpdateRegion;
internal sealed class UpdateRegionCommandHandler : ICommandHandler<UpdateRegionCommand, RegionDetailDto>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateRegionCommandHandler(IRepositoryBase<Region, Guid> regionRepository, ICurrentUser currentUser, IMapper mapper)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<RegionDetailDto>> Handle(UpdateRegionCommand request, CancellationToken cancellationToken)
    {
        var regionEntity = await _regionRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new RegionNotFoundException(request.Id);

        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code;

        if (code is not null && await _regionRepository.FindAll(x => x.Code == code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<RegionDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(Region), code));

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        regionEntity.Update(code, request.Name, request.Note, request.SortOrder, _currentUser.UserId.ToString());
        regionEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name, t.Note)));

        var result = _mapper.Map<RegionDetailDto>(regionEntity);

        return Result.Success(result);
    }
}
