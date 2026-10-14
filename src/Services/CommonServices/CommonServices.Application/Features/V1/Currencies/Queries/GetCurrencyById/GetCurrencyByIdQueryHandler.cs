using AutoMapper;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CurrencyException;

namespace CommonServices.Application.Features.V1.Currencies.Queries.GetCurrencyById;
public class GetCurrencyByIdQueryHandler : IQueryHandler<GetCurrencyByIdQuery, CurrencyDetailDto>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly IMapper _mapper;

    public GetCurrencyByIdQueryHandler(IRepositoryBase<Currency, Guid> currencyRepository, IMapper mapper)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CurrencyDetailDto>> Handle(GetCurrencyByIdQuery request, CancellationToken cancellationToken)
    {
        var currency = await _currencyRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CurrencyNotFoundException(request.Id);

        var result = _mapper.Map<CurrencyDetailDto>(currency);

        return Result.Success(result);
    }
}
