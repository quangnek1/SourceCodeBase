using AutoMapper;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Regions.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<RegionTranslation, RegionTranslationDto>();
        CreateMap<Region, RegionDetailDto>();
    }
}
