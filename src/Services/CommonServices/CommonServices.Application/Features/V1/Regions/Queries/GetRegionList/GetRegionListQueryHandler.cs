using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Application.Features.V1.Regions.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegionList;
public class GetRegionListQueryHandler : IQueryHandler<GetRegionListQuery, IReadOnlyList<RegionDto>>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;

    public GetRegionListQueryHandler(IRepositoryBase<Region, Guid> regionRepository)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
    }

    public async Task<Result<IReadOnlyList<RegionDto>>> Handle(GetRegionListQuery request, CancellationToken cancellationToken)
    {
        var regionsQuery = request.Status is { } status
            ? _regionRepository.FindAll(x => x.Status == status)
            : _regionRepository.FindAll();

        var result = await regionsQuery
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(RegionExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RegionDto>>(result);
    }
}
