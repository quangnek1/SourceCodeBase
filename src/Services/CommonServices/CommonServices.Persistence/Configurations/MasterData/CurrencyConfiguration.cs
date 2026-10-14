using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class CurrencyConfiguration : MasterDataEntityConfiguration<Currency, CurrencyTranslation>
{
    protected override string TableName => TableNames.Currencies;

    protected override void ConfigureEntity(EntityTypeBuilder<Currency> builder)
    {
        builder.Property(x => x.Symbol).HasMaxLength(5);
        builder.Property(x => x.IsCurrencyOptionEnabled).HasDefaultValue(false);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.CurrencyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CurrencyTranslationConfiguration : EntityTranslationConfiguration<CurrencyTranslation>
{
    protected override string TableName => TableNames.CurrencyTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<CurrencyTranslation> builder)
    {
        builder.HasKey(x => new { x.CurrencyId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired(true);
    }
}
