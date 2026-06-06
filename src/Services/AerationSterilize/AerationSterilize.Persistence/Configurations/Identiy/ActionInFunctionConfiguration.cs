using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AerationSterilize.Persistence.Configurations.Identiy;
internal sealed class ActionInFunctionConfiguration : IEntityTypeConfiguration<ActionInFunction>
{
    public void Configure(EntityTypeBuilder<ActionInFunction> builder)
    {
        builder.ToTable(TableNames.ActionInFunctions);

        builder.HasKey(x => new { x.ActionId, x.FunctionId });
    }
}
