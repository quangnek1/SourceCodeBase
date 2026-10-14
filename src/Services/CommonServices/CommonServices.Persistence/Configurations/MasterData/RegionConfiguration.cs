using CommonServices.Domain.Entities.MasterData;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;
internal sealed class RegionConfiguration : MasterDataEntityConfiguration<Region, RegionTranslation>
{
    protected override string TableName => TableNames.Regions;

    // The legacy system only checked the region code when one was entered.
    protected override bool IsCodeRequired => false;

    protected override void ConfigureEntity(EntityTypeBuilder<Region> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);

        builder.HasMany(x => x.Translations)
            .WithOne()
            .HasForeignKey(t => t.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RegionTranslationConfiguration : EntityTranslationConfiguration<RegionTranslation>
{
    protected override string TableName => TableNames.RegionTranslations;

    protected override void ConfigureTranslation(EntityTypeBuilder<RegionTranslation> builder)
    {
        builder.HasKey(x => new { x.RegionId, x.LangCode });

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired(true);
        builder.Property(x => x.Note).HasMaxLength(500);
    }
}
