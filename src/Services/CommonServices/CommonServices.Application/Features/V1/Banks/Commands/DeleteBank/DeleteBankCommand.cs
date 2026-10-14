using CommonServices.Application.Features.V1.Banks.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Banks.Commands.DeleteBank;
public sealed record DeleteBankCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];
}
