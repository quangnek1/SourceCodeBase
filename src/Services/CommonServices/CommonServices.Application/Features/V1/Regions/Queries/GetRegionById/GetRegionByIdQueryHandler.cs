using AutoMapper;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.RegionException;

namespace CommonServices.Application.Features.V1.Regions.Queries.GetRegionById;
public class GetRegionByIdQueryHandler : IQueryHandler<GetRegionByIdQuery, RegionDetailDto>
{
    private readonly IRepositoryBase<Region, Guid> _regionRepository;
    private readonly IMapper _mapper;

    public GetRegionByIdQueryHandler(IRepositoryBase<Region, Guid> regionRepository, IMapper mapper)
    {
        _regionRepository = regionRepository ?? throw new ArgumentNullException(nameof(regionRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<RegionDetailDto>> Handle(GetRegionByIdQuery request, CancellationToken cancellationToken)
    {
        var region = await _regionRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new RegionNotFoundException(request.Id);

        var result = _mapper.Map<RegionDetailDto>(region);

        return Result.Success(result);
    }
}
