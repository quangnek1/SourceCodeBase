using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Common.Extensions;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Messages;
using Contracts.Common.Repositories;
using Contracts.Responses;
using Shared.Emumerations;
using Shared.Paging;

namespace CommonServices.Application.Features.V1.Banks.Queries.GetBanks;
public class GetBanksQueryHandler : IQueryHandler<GetBanksQuery, PagedResult<BankDto>>
{
    private readonly IRepositoryBase<Bank, Guid> _bankRepository;

    public GetBanksQueryHandler(IRepositoryBase<Bank, Guid> bankRepository)
    {
        _bankRepository = bankRepository ?? throw new ArgumentNullException(nameof(bankRepository));
    }

    public async Task<Result<PagedResult<BankDto>>> Handle(GetBanksQuery request, CancellationToken cancellationToken)
    {
        var banksQuery = _bankRepository.FindAll();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.Trim();
            banksQuery = banksQuery.Where(x => x.Code!.Contains(searchTerm)
                || x.Name.Contains(searchTerm)
                || x.Translations.Any(t => t.Name.Contains(searchTerm)));
        }

        if (request.Status is { } status)
            banksQuery = banksQuery.Where(x => x.Status == status);

        var sortExpression = BankExtension.GetSortExpression(request.SortColumn);

        banksQuery = request.SortOrder == SortOrder.Descending
            ? banksQuery.OrderByDescending(sortExpression)
            : banksQuery.OrderBy(sortExpression);

        var result = await banksQuery
            .Select(BankExtension.ToDto(request.LangCode))
            .ToPagedResultAsync(request.PageIndex, request.PageSize);

        return Result.Success(result);
    }
}
