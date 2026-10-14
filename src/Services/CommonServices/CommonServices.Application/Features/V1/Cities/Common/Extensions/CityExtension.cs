using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Cities.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Cities.Common.Extensions;
public static class CityExtension
{
    public static Expression<Func<City, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "country" => x => x.Country.Name,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.Name
        };

    // Translated in SQL: the names in the requested language, or the default-language names when there is no translation.
    public static Expression<Func<City, CityDto>> ToDto(string langCode)
        => x => new CityDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.CountryId,
            x.Country.Code!,
            x.Country.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Country.Name,
            x.Status);
}
