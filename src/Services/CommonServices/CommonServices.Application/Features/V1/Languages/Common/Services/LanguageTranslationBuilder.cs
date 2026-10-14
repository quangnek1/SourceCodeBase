using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Constants;
using CommonServices.Domain.Entities.MasterData;
using Contracts.Common.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Application.Features.V1.Languages.Common.Services;

// Same rules as the legacy ManageLanguageController:
// the default language always has a translation (taken from Name/Description when it was not sent), and every other
// existing language gets one too, filled with the default values until someone translates it.
internal static class LanguageTranslationBuilder
{
    public static async Task<List<(string LangCode, string Name, string? Description)>> BuildAsync(
        IRepositoryBase<Language, Guid> languageRepository,
        string name,
        string? description,
        IEnumerable<LanguageTranslationRequest>? requested,
        CancellationToken cancellationToken)
    {
        var translations = (requested ?? [])
            .Select(t => (t.LangCode, t.Name, t.Description))
            .ToList();

        var existingCodes = await languageRepository.FindAll()
            .Select(x => x.Code!)
            .ToListAsync(cancellationToken);

        foreach (var code in existingCodes.Prepend(LanguageCodes.Default).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!translations.Exists(t => string.Equals(t.LangCode, code, StringComparison.OrdinalIgnoreCase)))
                translations.Add((code, name, description));
        }

        return translations;
    }
}
