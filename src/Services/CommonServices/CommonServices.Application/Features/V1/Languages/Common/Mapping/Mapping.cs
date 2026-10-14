using AutoMapper;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Languages.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<LanguageTranslation, LanguageTranslationDto>();
        CreateMap<Language, LanguageDetailDto>();
    }
}
