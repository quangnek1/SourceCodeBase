using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class CurrencyTranslation : EntityTranslation
{
    public Guid CurrencyId { get; private set; }
    public string Name { get; private set; } = null!;

    private CurrencyTranslation() { }

    internal CurrencyTranslation(Guid currencyId, string langCode, string name)
    {
        CurrencyId = currencyId;
        LangCode = langCode;
        Update(name);
    }

    internal void Update(string name) => Name = name;
}
