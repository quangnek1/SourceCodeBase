using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class City : MasterDataEntity<CityTranslation>
{
    public Guid CountryId { get; private set; }

    public Country Country { get; private set; } = null!;

    private City() { }

    public static City Create(string code, string name, Guid countryId, string? userId)
    {
        var city = new City();
        city.MarkCreated(userId);
        city.SetValues(code, name, countryId);

        return city;
    }

    public void Update(string code, string name, Guid countryId, string? userId)
    {
        SetValues(code, name, countryId);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new CityTranslation(Id, t.LangCode, t.Name),
            (existing, t) => existing.Update(t.Name));

    private void SetValues(string code, string name, Guid countryId)
    {
        Code = code;
        Name = name;
        CountryId = countryId;
    }
}
