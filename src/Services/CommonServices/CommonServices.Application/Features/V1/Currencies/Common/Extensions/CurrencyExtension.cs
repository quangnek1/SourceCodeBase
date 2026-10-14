using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Currencies.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Currencies.Common.Extensions;
public static class CurrencyExtension
{
    public static Expression<Func<Currency, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "symbol" => x => x.Symbol!,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.Code!
        };

    // Translated in SQL: the name in the requested language, or the default-language name when there is no translation.
    public static Expression<Func<Currency, CurrencyDto>> ToDto(string langCode)
        => x => new CurrencyDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.Symbol,
            x.IsCurrencyOptionEnabled,
            x.Status);
}
