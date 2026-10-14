using CommonServices.Application.Features.V1.Languages.Common.Caching;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Enums;
using Contracts.Caching;
using Contracts.Common.Messages;

namespace CommonServices.Application.Features.V1.Languages.Commands.UpdateLanguage;
public sealed class UpdateLanguageCommand : ICommand<LanguageDetailDto>, ICacheInvalidator
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? FlagImagePath { get; set; }
    public LanguagePlatform? Platform { get; set; }
    public IReadOnlyList<LanguageTranslationRequest>? Translations { get; set; }

    IEnumerable<string> ICacheInvalidator.CacheKeyPrefixes => [LanguageCacheKeys.Prefix];

    public void SetId(Guid id)
    {
        Id = id;
    }
}
