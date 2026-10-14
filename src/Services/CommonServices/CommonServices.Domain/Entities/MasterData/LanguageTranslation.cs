using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class LanguageTranslation : EntityTranslation
{
    public Guid LanguageId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    private LanguageTranslation() { }

    internal LanguageTranslation(Guid languageId, string langCode, string name, string? description)
    {
        LanguageId = languageId;
        LangCode = langCode;
        Update(name, description);
    }

    internal void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
