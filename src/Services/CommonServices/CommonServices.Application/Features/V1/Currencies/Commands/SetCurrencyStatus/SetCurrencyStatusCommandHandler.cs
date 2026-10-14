using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CurrencyException;

namespace CommonServices.Application.Features.V1.Currencies.Commands.SetCurrencyStatus;
internal sealed class SetCurrencyStatusCommandHandler : ICommandHandler<SetCurrencyStatusCommand>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;

    public SetCurrencyStatusCommandHandler(IRepositoryBase<Currency, Guid> currencyRepository, ICurrentUser currentUser)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetCurrencyStatusCommand request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _currencyRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CurrencyNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
            currencyEntity.Activate(_currentUser.UserId.ToString());
        else
            currencyEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
