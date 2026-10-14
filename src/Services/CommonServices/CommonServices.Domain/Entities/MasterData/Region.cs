using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class Region : MasterDataEntity<RegionTranslation>
{
    public string? Note { get; private set; }
    public int SortOrder { get; private set; }

    private Region() { }

    public static Region Create(string? code, string name, string? note, int sortOrder, string? userId)
    {
        var region = new Region();
        region.MarkCreated(userId);
        region.SetValues(code, name, note, sortOrder);

        return region;
    }

    public void Update(string? code, string name, string? note, int sortOrder, string? userId)
    {
        SetValues(code, name, note, sortOrder);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name, string? Note)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new RegionTranslation(Id, t.LangCode, t.Name, t.Note),
            (existing, t) => existing.Update(t.Name, t.Note));

    private void SetValues(string? code, string name, string? note, int sortOrder)
    {
        Code = code;
        Name = name;
        Note = note;
        SortOrder = sortOrder;
    }
}
