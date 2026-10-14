using CommonServices.Application.Features.V1.Languages.Common.Caching;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Languages.Commands.DeleteLanguage;
public sealed record DeleteLanguageCommand(Guid Id) : ICommand, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];
}
