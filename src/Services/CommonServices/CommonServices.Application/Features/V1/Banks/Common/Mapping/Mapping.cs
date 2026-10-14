using AutoMapper;
using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Banks.Common.Mapping;
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<BankTranslation, TranslationDto>();
        CreateMap<Bank, BankDetailDto>();
    }
}
