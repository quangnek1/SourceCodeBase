using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Application.Features.V1.Languages.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Languages.Queries.GetLanguages;
public class GetLanguagesQueryHandler : IQueryHandler<GetLanguagesQuery, PagedResult<LanguageDto>>
{
    private readonly IRepositoryBase<Language, Guid> _languageRepository;

    public GetLanguagesQueryHandler(IRepositoryBase<Language, Guid> languageRepository)
    {
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
    }

    public async Task<Result<PagedResult<LanguageDto>>> Handle(GetLanguagesQuery request, CancellationToken cancellationToken)
    {
        var languagesQuery = _languageRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            languagesQuery = languagesQuery.Where(x => x.Code!.Contains(searchTerm)
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            languagesQuery = languagesQuery.Where(x => x.Status == status);

        var sortExpression = LanguageExtension.GetSortExpression(request.SortColumn);

        languagesQuery = request.SortOrder == SortOrder.Descending
            ? languagesQuery.OrderByDescending(sortExpression)
            : languagesQuery.OrderBy(sortExpression);

        var result = await languagesQuery
            .Select(LanguageExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
