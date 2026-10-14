using AutoMapper;
using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Countries.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CountryTranslation, TranslationDto>();
        CreateMap<Country, CountryDetailDto>();
    }
}
