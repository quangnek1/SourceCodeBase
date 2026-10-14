using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class Currency : MasterDataEntity<CurrencyTranslation>
{
    public string? Symbol { get; private set; }

    /// <summary>
    /// Cho phép chọn tiền tệ này khi hiển thị giá
    /// </summary>
    public bool IsCurrencyOptionEnabled { get; private set; }

    private Currency() { }

    public static Currency Create(string code, string name, string? symbol, bool isCurrencyOptionEnabled, string? userId)
    {
        var currency = new Currency();
        currency.MarkCreated(userId);
        currency.SetValues(code, name, symbol, isCurrencyOptionEnabled);

        return currency;
    }

    public void Update(string code, string name, string? symbol, bool isCurrencyOptionEnabled, string? userId)
    {
        SetValues(code, name, symbol, isCurrencyOptionEnabled);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new CurrencyTranslation(Id, t.LangCode, t.Name),
            (existing, t) => existing.Update(t.Name));

    private void SetValues(string code, string name, string? symbol, bool isCurrencyOptionEnabled)
    {
        Code = code;
        Name = name;
        Symbol = symbol;
        IsCurrencyOptionEnabled = isCurrencyOptionEnabled;
    }
}
