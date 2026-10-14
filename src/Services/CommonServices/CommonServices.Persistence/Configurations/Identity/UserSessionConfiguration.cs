using CommonServices.Domain.Entities.Identity;
using CommonServices.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommonServices.Persistence.Configurations.Identity;
public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable(TableNames.UserSessions);

        builder.HasKey(t => t.Id);
        builder.Property(x => x.RefreshToken).HasMaxLength(500);
    }
}
