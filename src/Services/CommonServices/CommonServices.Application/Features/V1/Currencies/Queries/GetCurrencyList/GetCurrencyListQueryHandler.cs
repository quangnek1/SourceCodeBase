using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Application.Features.V1.Currencies.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyList;
public class GetCurrencyListQueryHandler : IQueryHandler<GetCurrencyListQuery, IReadOnlyList<CurrencyDto>>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;

    public GetCurrencyListQueryHandler(IRepositoryBase<Currency, Guid> currencyRepository)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
    }

    public async Task<Result<IReadOnlyList<CurrencyDto>>> Handle(GetCurrencyListQuery request, CancellationToken cancellationToken)
    {
        var currenciesQuery = request.Status is { } status
            ? _currencyRepository.FindAll(x => x.Status == status)
            : _currencyRepository.FindAll();

        var result = await currenciesQuery
            .OrderBy(x => x.Code)
            .Select(CurrencyExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CurrencyDto>>(result);
    }
}
