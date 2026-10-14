using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class CityConfiguration : MasterDataEntityConfiguration<City, CityTranslation>
{
    protected override string TableName => TableNames.Cities;

    protected override void ConfigureEntity(EntityTypeBuilder<City> builder)
    {
        builder.HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.CityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CityTranslationConfiguration : EntityTranslationConfiguration<CityTranslation>
{
    protected override string TableName => TableNames.CityTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<CityTranslation> builder)
    {
        builder.HasKey(x => new { x.CityId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired(true);
    }
}
