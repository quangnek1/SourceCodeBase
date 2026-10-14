using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class CountryConfiguration : MasterDataEntityConfiguration<Country, CountryTranslation>
{
    protected override string TableName => TableNames.Countries;

    protected override void ConfigureEntity(EntityTypeBuilder<Country> builder)
    {
        builder.HasOne(x => x.Region)
            .WithMany()
            .HasForeignKey(x => x.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.CountryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CountryTranslationConfiguration : EntityTranslationConfiguration<CountryTranslation>
{
    protected override string TableName => TableNames.CountryTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<CountryTranslation> builder)
    {
        builder.HasKey(x => new { x.CountryId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired(true);
    }
}
