using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencies;
public class GetCurrenciesQueryHandler : IQueryHandler<GetCurrenciesQuery, PagedResult<CurrencyDto>>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;

    public GetCurrenciesQueryHandler(IRepositoryBase<Currency, Guid> currencyRepository)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
    }

    public async Task<Result<PagedResult<CurrencyDto>>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var currenciesQuery = _currencyRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            currenciesQuery = currenciesQuery.Where(x => x.Code!.Contains(searchTerm)
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            currenciesQuery = currenciesQuery.Where(x => x.Status == status);

        var sortExpression = CurrencyExtension.GetSortExpression(request.SortColumn);

        currenciesQuery = request.SortOrder == SortOrder.Descending
            ? currenciesQuery.OrderByDescending(sortExpression)
            : currenciesQuery.OrderBy(sortExpression);

        var result = await currenciesQuery
            .Select(CurrencyExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
