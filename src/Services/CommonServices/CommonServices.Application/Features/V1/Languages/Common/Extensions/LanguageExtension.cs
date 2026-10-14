using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Languages.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Languages.Common.Extensions;
public static class LanguageExtension
{
    public static Expression<Func<Language, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.Code!
        };

    // Translated in SQL: the text in the requested language, or the default-language text when there is no translation.
    public static Expression<Func<Language, LanguageDto>> ToDto(string langCode)
        => x => new LanguageDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Description).FirstOrDefault() ?? x.Description,
            x.FlagImagePath,
            x.Platform,
            x.Status);

    // Each language named in itself ("Tiếng Việt", "English"...), used by language pickers.
    public static Expression<Func<Language, LanguageDto>> ToNativeDto()
        => x => new LanguageDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == x.Code).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.Translations.Where(t => t.LangCode == x.Code).Select(t => t.Description).FirstOrDefault() ?? x.Description,
            x.FlagImagePath,
            x.Platform,
            x.Status);
}
