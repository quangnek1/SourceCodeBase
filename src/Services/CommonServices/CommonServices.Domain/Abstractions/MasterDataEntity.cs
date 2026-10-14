using CommonServices.Domain.Enums;
using Contracts.Abstractions.Entities.Domains;
using Contracts.Abstractions.Entities.Domains.Interfaces;

namespace CommonServices.Domain.Abstractions;

/// <summary>
/// Base of the master data entities (Bank, Country, City...): code, default-language name, status and translations.
/// Deleting only flags the row as <see cref="ObjectStatus.Deleted"/>, like the legacy system.
/// </summary>
public abstract class MasterDataEntity<TTranslation> : EntityAuditBase<Guid>, IUserTracking
    where TTranslation : EntityTranslation
{
    // Not private: EF looks the backing field up on the concrete entity type, where a private base field is invisible.
    private protected readonly List<TTranslation> _translations = [];

    public string? Code { get; protected set; }
    public string Name { get; protected set; } = null!;
    public ObjectStatus Status { get; protected set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }

    public IReadOnlyCollection<TTranslation> Translations => _translations;

    public void Activate(string? userId) => ChangeStatus(ObjectStatus.Active, userId);

    public void Deactivate(string? userId) => ChangeStatus(ObjectStatus.Inactive, userId);

    public void Delete(string? userId) => ChangeStatus(ObjectStatus.Deleted, userId);

    protected void MarkCreated(string? userId)
    {
        Id = Guid.NewGuid();
        Status = ObjectStatus.Active;
        CreatedDate = DateTimeOffset.UtcNow;
        CreatedBy = userId;
    }

    protected void MarkModified(string? userId)
    {
        LastModifiedDate = DateTimeOffset.UtcNow;
        LastModifiedBy = userId;
    }

    // Updates the existing rows in place (EF tracks them by key), adds the new languages and removes the ones not sent.
    protected void SyncTranslations<TValue>(
        IEnumerable<TValue> values,
        Func<TValue, string> getLangCode,
        Func<TValue, TTranslation> create,
        Action<TTranslation, TValue> update)
    {
        var incoming = values
            .GroupBy(getLangCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        _translations.RemoveAll(t => !incoming.ContainsKey(t.LangCode));

        foreach (var (langCode, value) in incoming)
        {
            var existing = _translations.FirstOrDefault(t => string.Equals(t.LangCode, langCode, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                _translations.Add(create(value));
            else
                update(existing, value);
        }
    }

    private void ChangeStatus(ObjectStatus status, string? userId)
    {
        Status = status;
        MarkModified(userId);
    }
}
