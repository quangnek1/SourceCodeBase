using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Countries.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Countries.Common.Extensions;
public static class CountryExtension
{
    public static Expression<Func<Country, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.Name
        };

    // Translated in SQL: the names in the requested language, or the default-language names when there is no translation.
    // A deleted region/currency is hidden by its query filter, so RegionName/CurrencyCode come back null for it.
    public static Expression<Func<Country, CountryDto>> ToDto(string langCode)
        => x => new CountryDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.RegionId,
            x.Region == null
                ? null
                : x.Region.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Region.Name,
            x.CurrencyId,
            x.Currency == null ? null : x.Currency.Code,
            x.Status);
}
