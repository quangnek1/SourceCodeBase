using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using CommonServices.Domain.Enums;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.BankException;

namespace CommonServices.Application.Features.V1.Banks.Commands.SetBankStatus;
internal sealed class SetBankStatusCommandHandler : ICommandHandler<SetBankStatusCommand>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;
    private readonly ICurrentUser _currentUser;

    public SetBankStatusCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(SetBankStatusCommand request, CancellationToken cancellationToken)
    {
        var bankEntity = await _bankRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new BankNotFoundException(request.Id);

        if (request.Status == ObjectStatus.Active)
            bankEntity.Activate(_currentUser.UserId.ToString());
        else
            bankEntity.Deactivate(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
