using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class CountryTranslation : EntityTranslation
{
    public Guid CountryId { get; private set; }
    public string Name { get; private set; } = null!;

    private CountryTranslation() { }

    internal CountryTranslation(Guid countryId, string langCode, string name)
    {
        CountryId = countryId;
        LangCode = langCode;
        Update(name);
    }

    internal void Update(string name) => Name = name;
}
