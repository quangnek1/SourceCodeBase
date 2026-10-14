using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class LanguageConfiguration : MasterDataEntityConfiguration<Language, LanguageTranslation>
{
    protected override string TableName => TableNames.Languages;

    protected override int NameMaxLength => 128;

    protected override void ConfigureEntity(EntityTypeBuilder<Language> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(256);
        builder.Property(x => x.FlagImagePath).HasMaxLength(1000);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LanguageTranslationConfiguration : EntityTranslationConfiguration<LanguageTranslation>
{
    protected override string TableName => TableNames.LanguageTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<LanguageTranslation> builder)
    {
        builder.HasKey(x => new { x.LanguageId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(128).IsRequired(true);
        builder.Property(x => x.Description).HasMaxLength(256);
    }
}
