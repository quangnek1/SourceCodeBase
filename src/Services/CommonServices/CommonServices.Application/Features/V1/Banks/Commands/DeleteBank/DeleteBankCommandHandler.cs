using CommonServices.Application.Common;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.BankException;

namespace CommonServices.Application.Features.V1.Banks.Commands.DeleteBank;
internal sealed class DeleteBankCommandHandler : ICommandHandler<DeleteBankCommand>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteBankCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result> Handle(DeleteBankCommand request, CancellationToken cancellationToken)
    {
        var bankEntity = await _bankRepository.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new BankNotFoundException(request.Id);

        // Soft delete: the row is kept with Status = Deleted.
        bankEntity.Delete(_currentUser.UserId.ToString());

        return Result.Success();
    }
}
