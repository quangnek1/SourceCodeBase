using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class RegionTranslation : EntityTranslation
{
    public Guid RegionId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Note { get; private set; }

    private RegionTranslation() { }

    internal RegionTranslation(Guid regionId, string langCode, string name, string? note)
    {
        RegionId = regionId;
        LangCode = langCode;
        Update(name, note);
    }

    internal void Update(string name, string? note)
    {
        Name = name;
        Note = note;
    }
}
