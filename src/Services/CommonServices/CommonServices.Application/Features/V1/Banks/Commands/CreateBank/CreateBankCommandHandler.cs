using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Banks.Commands.CreateBank;
internal sealed class CreateBankCommandHandler : ICommandHandler<CreateBankCommand, Guid>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;
    private readonly ICurrentUser _currentUser;

    public CreateBankCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<Guid>> Handle(CreateBankCommand request, CancellationToken cancellationToken)
    {
        if (await _bankRepository.FindAll(x => x.Code == request.Code).AnyAsync(cancellationToken))
            return Result.Failure<Guid>(MasterDataErrors.CodeAlreadyExists(nameof(Bank), request.Code));

        var bankEntity = Bank.Create(request.Code, request.Name, request.VAPrefix, request.IsVAByBank, _currentUser.UserId.ToString());
        bankEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        _bankRepository.Add(bankEntity);

        return Result.Success(bankEntity.Id);
    }
}
