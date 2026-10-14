using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBankList;
public class GetBankListQueryHandler : IQueryHandler<GetBankListQuery, IReadOnlyList<BankDto>>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;

    public GetBankListQueryHandler(IRepositoryBase<Bank, Guid> bankRepository)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
    }

    public async Task<Result<IReadOnlyList<BankDto>>> Handle(GetBankListQuery request, CancellationToken cancellationToken)
    {
        var banksQuery = request.Status is { } status
            ? _bankRepository.FindAll(x => x.Status == status)
            : _bankRepository.FindAll();

        var result = await banksQuery
            .OrderBy(x => x.Name)
            .Select(BankExtension.ToDto(request.LangCode))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BankDto>>(result);
    }
}
