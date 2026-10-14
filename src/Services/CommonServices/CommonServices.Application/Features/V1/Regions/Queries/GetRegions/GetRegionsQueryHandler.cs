using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Application.Features.V1.Regions.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegions;
public class GetRegionsQueryHandler : IQueryHandler<GetRegionsQuery, PagedResult<RegionDto>>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;

    public GetRegionsQueryHandler(IRepositoryBase<Region, Guid> regionRepository)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
    }

    public async Task<Result<PagedResult<RegionDto>>> Handle(GetRegionsQuery request, CancellationToken cancellationToken)
    {
        var regionsQuery = _regionRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            regionsQuery = regionsQuery.Where(x => (x.Code != null && x.Code.Contains(searchTerm))
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            regionsQuery = regionsQuery.Where(x => x.Status == status);

        var sortExpression = RegionExtension.GetSortExpression(request.SortColumn);

        regionsQuery = request.SortOrder == SortOrder.Descending
            ? regionsQuery.OrderByDescending(sortExpression)
            : regionsQuery.OrderBy(sortExpression);

        var result = await regionsQuery
            .Select(RegionExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
