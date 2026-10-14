using CommonServices.Domain.Abstractions;
using CommonServices.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.MasterData;

// Columns shared by every master data table. Each entity configuration only adds its own columns and the translation FK.
internal abstract class MasterDataEntityConfiguration<TEntity, TTranslation> : IEntityTypeConfiguration<TEntity>
    where TEntity : MasterDataEntity<TTranslation>
    where TTranslation : EntityTranslation
{
    protected abstract string TableName { get; }

    protected virtual int NameMaxLength => 256;

    protected virtual bool IsCodeRequired => true;

    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever(); // set by the domain, see MasterDataEntity.MarkCreated

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired(IsCodeRequired);
        builder.Property(x => x.Name).HasMaxLength(NameMaxLength).IsRequired(true);
        builder.Property(x => x.Status).IsRequired(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(50);
        builder.Property(x => x.LastModifiedBy).HasMaxLength(50);

        // Deleted rows stay in the table but are hidden from every query.
        builder.HasQueryFilter(x => x.Status != ObjectStatus.Deleted);

        // The code only has to be unique among the rows that are not deleted.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter($"[Code] IS NOT NULL AND [Status] <> {(int)ObjectStatus.Deleted}");

        builder.Navigation(x => x.Translations)
            .HasField("_translations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        ConfigureEntity(builder);
    }

    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}

internal abstract class EntityTranslationConfiguration<TTranslation> : IEntityTypeConfiguration<TTranslation>
    where TTranslation : EntityTranslation
{
    protected abstract string TableName { get; }

    public void Configure(EntityTypeBuilder<TTranslation> builder)
    {
        builder.ToTable(TableName);

        builder.Property(x => x.LangCode).HasMaxLength(10).IsRequired(true);

        ConfigureTranslation(builder);
    }

    protected abstract void ConfigureTranslation(EntityTypeBuilder<TTranslation> builder);
}
