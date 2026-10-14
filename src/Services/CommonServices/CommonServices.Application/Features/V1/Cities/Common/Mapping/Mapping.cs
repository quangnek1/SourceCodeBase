using AutoMapper;
using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Cities.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CityTranslation, TranslationDto>();
        CreateMap<City, CityDetailDto>();
    }
}
