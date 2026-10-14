using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class CityTranslation : EntityTranslation
{
    public Guid CityId { get; private set; }
    public string Name { get; private set; } = null!;

    private CityTranslation() { }

    internal CityTranslation(Guid cityId, string langCode, string name)
    {
        CityId = cityId;
        LangCode = langCode;
        Update(name);
    }

    internal void Update(string name) => Name = name;
}
