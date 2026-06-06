using AerationSterilize.Domain.Entities.Identity;
using AerationSterilize.Persistence.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AerationSterilize.Persistence.Configurations.Identiy;
public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable(TableNames.UserSessions);

        builder.HasKey(t => t.Id);
        builder.Property(x => x.RefreshToken).HasMaxLength(500);
    }
}
