using CommonServices.Domain.Abstractions;
using CommonServices.Domain.Enums;

namespace CommonServices.Domain.Entities.MasterData;
public class Language : MasterDataEntity<LanguageTranslation>
{
    public string? Description { get; private set; }
    public string? FlagImagePath { get; private set; }
    public LanguagePlatform? Platform { get; private set; }

    private Language() { }

    public static Language Create(string code, string name, string? description, string? flagImagePath, LanguagePlatform? platform, string? userId)
    {
        var language = new Language();
        language.MarkCreated(userId);
        language.SetValues(code, name, description, flagImagePath, platform);

        return language;
    }

    public void Update(string code, string name, string? description, string? flagImagePath, LanguagePlatform? platform, string? userId)
    {
        SetValues(code, name, description, flagImagePath, platform);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name, string? Description)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new LanguageTranslation(Id, t.LangCode, t.Name, t.Description),
            (existing, t) => existing.Update(t.Name, t.Description));

    private void SetValues(string code, string name, string? description, string? flagImagePath, LanguagePlatform? platform)
    {
        Code = code;
        Name = name;
        Description = description;
        FlagImagePath = flagImagePath;
        Platform = platform;
    }
}
