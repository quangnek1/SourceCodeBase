using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Languages.Commands.CreateLanguage;
public sealed record CreateLanguageCommand(
    string Code,
    string Name,
    string? Description,
    string? FlagImagePath,
    LanguagePlatform? Platform,
    IReadOnlyList<LanguageTranslationRequest>? Translations
) : ICommand<Guid>, ICacheInvalidator
{
    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];
}
