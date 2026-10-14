using CommonServices.Application.Features.V1.Banks.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Banks.Commands.SetBankStatus;
public sealed record SetBankStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [BankCacheKeys.Prefix];
}
