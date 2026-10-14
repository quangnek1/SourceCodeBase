using AutoMapper;
using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Currencies.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CurrencyTranslation, TranslationDto>();
        CreateMap<Currency, CurrencyDetailDto>();
    }
}
