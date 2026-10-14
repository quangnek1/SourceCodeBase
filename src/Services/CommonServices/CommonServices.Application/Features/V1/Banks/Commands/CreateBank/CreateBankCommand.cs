using CommonServices.Application.Common.Dtos;
using CommonServices.Application.Features.V1.Banks.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Banks.Commands.CreateBank;
public sealed record CreateBankCommand(
    string Code,
    string Name,
    string? VAPrefix,
    bool IsVAByBank,
    IReadOnlyList<TranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];
}
