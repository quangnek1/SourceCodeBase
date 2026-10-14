using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.CurrencyException;

namespace CommonServices.Application.Features.V1.Currencies.Commands.DeleteCurrency;
internal sealed class DeleteCurrencyCommandHandler : ICommandHandler<DeleteCurrencyCommand>
{
    private readonly IRepositoryBase<Currency, Guid> _currencyRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteCurrencyCommandHandler(IRepositoryBase<Currency, Guid> currencyRepository, ICurrentUser currentUser)
    {
        _currencyRepository = currencyRepository ?? throw new ArgumentNullException(nameof(currencyRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currencyEntity = await _currencyRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new CurrencyNotFoundException(request.Id);

        // Soft delete: the row is kept with Status = Deleted.
        currencyEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
