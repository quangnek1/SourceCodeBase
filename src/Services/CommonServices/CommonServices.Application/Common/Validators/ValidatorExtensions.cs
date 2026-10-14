using CommonServices.Application.Common.Dtos;
using CommonServices.Domain.Enums;
using FluentValidation;
using Shared.Paging;

namespace CommonServices.Application.Common.Validators;
public static class ValidatorExtensions
{
    public const int CodeMaxLength = 50;
    public const int NameMaxLength = 256;
    public const int LangCodeMaxLength = 10;

    public static IRuleBuilderOptions<T, int> MustBeValidPageIndex<T>(this IRuleBuilder<T, int> ruleBuilder)
        => ruleBuilder.GreaterThanOrEqualTo(PagedResult<object>.DefaultPageIndex);

    public static IRuleBuilderOptions<T, int> MustBeValidPageSize<T>(this IRuleBuilder<T, int> ruleBuilder)
        => ruleBuilder.InclusiveBetween(1, PagedResult<object>.UpperPageSize);

    // Activate/deactivate commands may only switch between these two; deleting has its own command.
    public static IRuleBuilderOptions<T, ObjectStatus> MustBeActiveOrInactive<T>(this IRuleBuilder<T, ObjectStatus> ruleBuilder)
        => ruleBuilder
            .Must(status => status is ObjectStatus.Active or ObjectStatus.Inactive)
            .WithMessage("Status must be Active or Inactive.");

    public static IRuleBuilderOptions<T, IReadOnlyList<TItem>?> MustHaveUniqueLangCodes<T, TItem>(
        this IRuleBuilder<T, IReadOnlyList<TItem>?> ruleBuilder)
        where TItem : ITranslationRequest
        => ruleBuilder
            .Must(items => items is null
                || items.Select(x => x.LangCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count)
            .WithMessage("Each language may only appear once in the translations.");
}
