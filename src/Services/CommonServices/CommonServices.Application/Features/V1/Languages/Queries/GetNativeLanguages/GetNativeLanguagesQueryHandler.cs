using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetNativeLanguages;
public class GetNativeLanguagesQueryHandler : IQueryHandler<GetNativeLanguagesQuery, IReadOnlyList<LanguageDto>>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;

    public GetNativeLanguagesQueryHandler(IRepositoryBase<Language, Guid> languageRepository)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
    }

    public async Task<Result<IReadOnlyList<LanguageDto>>> Handle(GetNativeLanguagesQuery request, CancellationToken cancellationToken)
    {
        var result = await _languageRepository.FindAll(x => x.Status == ObjectStatus.Active)
            .OrderBy(x => x.Code)
            .Select(LanguageExtension.ToNativeDto())
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LanguageDto>>(result);
    }
}
