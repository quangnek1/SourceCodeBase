using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguageList;
public class GetLanguageListQueryHandler : IQueryHandler<GetLanguageListQuery, IReadOnlyList<LanguageDto>>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;

    public GetLanguageListQueryHandler(IRepositoryBase<Language, Guid> languageRepository)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
    }

    public async Task<Result<IReadOnlyList<LanguageDto>>> Handle(GetLanguageListQuery request, CancellationToken cancellationToken)
    {
        var languagesQuery = request.Status is { } status
            ? _languageRepository.FindAll(x => x.Status == status)
            : _languageRepository.FindAll();

        var result = await languagesQuery
            .OrderBy(x => x.Code)
            .Select(LanguageExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<LanguageDto>>(result);
    }
}
