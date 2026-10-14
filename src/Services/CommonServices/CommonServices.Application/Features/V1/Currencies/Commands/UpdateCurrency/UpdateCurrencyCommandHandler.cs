using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.CurrencyException;

namespace CommonServices.Application.Features.V1.Currencies.Commands.UpdateCurrency;
internal sealed class UpdateCurrencyCommandHandler : ICommandHandler<UpdateCurrencyCommand, CurrencyDetailDto>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateCurrencyCommandHandler(IRepositoryBase<Currency, Guid> currencyRepository, ICurrentUser currentUser, IMapper mapper)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<CurrencyDetailDto>> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _currencyRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new CurrencyNotFoundException(request.Id);

        if (await _currencyRepository.FindAll(x => x.Code == request.Code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<CurrencyDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(Currency), request.Code));

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        currencyEntity.Update(request.Code, request.Name, request.Symbol, request.IsCurrencyOptionEnabled, _currentUser.UserId.ToString());
        currencyEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        var result = _mapper.Map<CurrencyDetailDto>(currencyEntity);

        return Result.Success(result);
    }
}
