using AutoMapper;
using CommonServices.Application.Common;
using CommonServices.Application.Common.Errors;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using static CommonServices.Domain.Exceptions.BankException;

namespace CommonServices.Application.Features.V1.Banks.Commands.UpdateBank;
internal sealed class UpdateBankCommandHandler : ICommandHandler<UpdateBankCommand, BankDetailDto>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public UpdateBankCommandHandler(IRepositoryBase<Bank, Guid> bankRepository, ICurrentUser currentUser, IMapper mapper)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<BankDetailDto>> Handle(UpdateBankCommand request, CancellationToken cancellationToken)
    {
        var bankEntity = await _bankRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new BankNotFoundException(request.Id);

        if (await _bankRepository.FindAll(x => x.Code == request.Code && x.Id != request.Id).AnyAsync(cancellationToken))
            return Result.Failure<BankDetailDto>(MasterDataErrors.CodeAlreadyExists(nameof(Bank), request.Code));

        // The entity is tracked, so the changes (translations included) are saved by the transaction behavior without calling Update().
        bankEntity.Update(request.Code, request.Name, request.IsVAByBank, _currentUser.UserId.ToString());
        bankEntity.SetTranslations((request.Translations ?? []).Select(t => (t.LangCode, t.Name)));

        var result = _mapper.Map<BankDetailDto>(bankEntity);

        return Result.Success(result);
    }
}
