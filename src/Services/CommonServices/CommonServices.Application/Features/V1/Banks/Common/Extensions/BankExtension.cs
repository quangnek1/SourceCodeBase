using System.Linq.Expressions;
using CommonServices.Application.Features.V1.Banks.Common.Dtos;
using CommonServices.Domain.Entities.MasterData;

namespace CommonServices.Application.Features.V1.Banks.Common.Extensions;
public static class BankExtension
{
    public static Expression<Func<Bank, object>> GetSortExpression(string? sortColumn)
        => sortColumn?.ToLower() switch
        {
            "code" => x => x.Code!,
            "name" => x => x.Name,
            "status" => x => x.Status,
            "createddate" => x => x.CreatedDate,
            _ => x => x.Name
        };

    // Translated in SQL: the name in the requested language, or the default-language name when there is no translation.
    public static Expression<Func<Bank, BankDto>> ToDto(string langCode)
        => x => new BankDto(
            x.Id,
            x.Code!,
            x.Translations.Where(t => t.LangCode == langCode).Select(t => t.Name).FirstOrDefault() ?? x.Name,
            x.VAPrefix,
            x.IsVAByBank,
            x.Status);
}
