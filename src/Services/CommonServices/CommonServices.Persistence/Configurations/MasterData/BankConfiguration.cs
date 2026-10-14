using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class BankConfiguration : MasterDataEntityConfiguration<Bank, BankTranslation>
{
    protected override string TableName => TableNames.Banks;

    protected override void ConfigureEntity(EntityTypeBuilder<Bank> builder)
    {
        builder.Property(x => x.VAPrefix).HasMaxLength(50);
        builder.Property(x => x.IsVAByBank).HasDefaultValue(false);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.BankId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BankTranslationConfiguration : EntityTranslationConfiguration<BankTranslation>
{
    protected override string TableName => TableNames.BankTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<BankTranslation> builder)
    {
        builder.HasKey(x => new { x.BankId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired(true);
    }
}
