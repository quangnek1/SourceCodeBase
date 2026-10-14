using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Regions.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Regions.Common.Extensions;
public static class RegionExtension
{
    public static Expression<Func<Region, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.SortOrder
        };

    // Translated in SQL: the text in the requested language, or the default-language text when there is no translation.
    public static Expression<Func<Region, RegionDto>> ToDto(string langCode)
        => x => new RegionDto(
            x.Id,
            x.Code,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Note).FirstOrDefault() ?? x.Note,
            x.SortOrder,
            x.Status);
}
