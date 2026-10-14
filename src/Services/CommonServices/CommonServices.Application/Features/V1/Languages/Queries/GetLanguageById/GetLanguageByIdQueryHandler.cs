using AutoMapper;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.LanguageException;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguageById;
public class GetLanguageByIdQueryHandler : IQueryHandler<GetLanguageByIdQuery, LanguageDetailDto>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;
    private readonly IMapper _mapper;

    public GetLanguageByIdQueryHandler(IRepositoryBase<Language, Guid> languageRepository, IMapper mapper)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<LanguageDetailDto>> Handle(GetLanguageByIdQuery request, CancellationToken cancellationToken)
    {
        var language = await _languageRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new LanguageNotFoundException(request.Id);

        var result = _mapper.Map<LanguageDetailDto>(language);

        return Result.Success(result);
    }
}
