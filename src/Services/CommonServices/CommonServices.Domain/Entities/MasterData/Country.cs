using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class Country : MasterDataEntity<CountryTranslation>
{
    public Guid? RegionId { get; private set; }
    public Guid? CurrencyId { get; private set; }

    public Region? Region { get; private set; }
    public Currency? Currency { get; private set; }

    private Country() { }

    public static Country Create(string code, string name, Guid? regionId, Guid? currencyId, string? userId)
    {
        var country = new Country();
        country.MarkCreated(userId);
        country.SetValues(code, name, regionId, currencyId);

        return country;
    }

    public void Update(string code, string name, Guid? regionId, Guid? currencyId, string? userId)
    {
        SetValues(code, name, regionId, currencyId);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new CountryTranslation(Id, t.LangCode, t.Name),
            (existing, t) => existing.Update(t.Name));

    private void SetValues(string code, string name, Guid? regionId, Guid? currencyId)
    {
        Code = code;
        Name = name;
        RegionId = regionId;
        CurrencyId = currencyId;
    }
}
