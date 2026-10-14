using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Common.Caching;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Banks.Commands.UpdateBank;

// VAPrefix is not part of the update: the legacy system never allowed changing it after creation.
public sealed class UpdateBankCommand : ICommand<BankDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsVAByBank { get; set; }
    public IReadOnlyList<TranslationRequest>? Translations { get; set; }

    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
