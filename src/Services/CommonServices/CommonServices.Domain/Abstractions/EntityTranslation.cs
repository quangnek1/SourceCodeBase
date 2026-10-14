namespace CommonServices.Domain.Abstractions;

// One row per language of a master data entity. Key = (owner id, LangCode).
public abstract class EntityTranslation
{
    public string LangCode { get; protected set; } = null!;
}
