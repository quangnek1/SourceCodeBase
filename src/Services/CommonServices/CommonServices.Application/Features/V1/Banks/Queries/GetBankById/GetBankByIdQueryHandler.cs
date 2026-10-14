using AutoMapper;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using static CommonServices.Domain.Exceptions.BankException;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBankById;
public class GetBankByIdQueryHandler : IQueryHandler<GetBankByIdQuery, BankDetailDto>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;
    private readonly IMapper _mapper;

    public GetBankByIdQueryHandler(IRepositoryBase<Bank, Guid> bankRepository, IMapper mapper)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<Result<BankDetailDto>> Handle(GetBankByIdQuery request, CancellationToken cancellationToken)
    {
        var bank = await _bankRepository.FindByIdAsync(request.Id, cancellationToken, x => x.Translations)
            ?? throw new BankNotFoundException(request.Id);

        var result = _mapper.Map<BankDetailDto>(bank);

        return Result.Success(result);
    }
}
