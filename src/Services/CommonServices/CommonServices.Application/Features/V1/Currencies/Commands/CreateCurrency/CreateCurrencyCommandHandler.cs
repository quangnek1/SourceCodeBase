using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Currencies.Commands.CreateCurrency;
internal sealed class CreateCurrencyCommandHandler : ICommandHandler<CreateCurrencyCommand, Guid>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;

    public CreateCurrencyCommandHandler(IRepositoryBase<Currency, Guid> currencyRepository, ICurrentUser currentUser)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (await _currencyRepository.FindAll(x => x.Code == request.Code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(Currency), request.Code));

        var currencyEntity = Currency.Create(request.Code, request.Name, request.Symbol, request.IsCurrencyOptionEnabled, _currentUser.UserId.ToString());
        currencyEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        _currencyRepository.Add(currencyEntity);

        return Result.Success(currencyEntity.Id);
    }
}
