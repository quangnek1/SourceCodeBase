using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class BankTranslation : EntityTranslation
{
    public Guid BankId { get; private set; }
    public string Name { get; private set; } = null!;

    private BankTranslation() { }

    internal BankTranslation(Guid bankId, string langCode, string name)
    {
        BankId = bankId;
        LangCode = langCode;
        Update(name);
    }

    internal void Update(string name) => Name = name;
}
