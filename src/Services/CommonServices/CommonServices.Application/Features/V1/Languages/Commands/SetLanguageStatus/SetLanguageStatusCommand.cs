using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Languages.Commands.SetLanguageStatus;
public sealed record SetLanguageStatusCommand(Guid Id, ObjectStatus Status) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];
}
