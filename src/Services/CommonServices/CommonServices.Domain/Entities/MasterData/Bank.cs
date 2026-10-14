using CommonServices.Domain.Abstractions;

namespace CommonServices.Domain.Entities.MasterData;
public class Bank : MasterDataEntity<BankTranslation>
{
    /// <summary>
    /// Tiền tố tài khoản ảo (Virtual Account). Chỉ gán khi tạo, không cho sửa.
    /// </summary>
    public string? VAPrefix { get; private set; }

    /// <summary>
    /// Tài khoản ảo do ngân hàng cấp
    /// </summary>
    public bool IsVAByBank { get; private set; }

    private Bank() { }

    public static Bank Create(string code, string name, string? vaPrefix, bool isVAByBank, string? userId)
    {
        var bank = new Bank
        {
            VAPrefix = vaPrefix
        };
        bank.MarkCreated(userId);
        bank.SetValues(code, name, isVAByBank);

        return bank;
    }

    public void Update(string code, string name, bool isVAByBank, string? userId)
    {
        SetValues(code, name, isVAByBank);
        MarkModified(userId);
    }

    public void SetTranslations(IEnumerable<(string LangCode, string Name)> translations)
        => SyncTranslations(
            translations,
            t => t.LangCode,
            t => new BankTranslation(Id, t.LangCode, t.Name),
            (existing, t) => existing.Update(t.Name));

    private void SetValues(string code, string name, bool isVAByBank)
    {
        Code = code;
        Name = name;
        IsVAByBank = isVAByBank;
    }
}
