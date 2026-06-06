using AerationSterilize.Domain.Entities;
using AerationSterilize.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AerationSterilize.Persistence;

public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
        => builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

    public DbSet<AppUser> AppUses { get; set; }
    public DbSet<Domain.Entities.Identity.Action> Actions { get; set; }
    public DbSet<Function> Functions { get; set; }
    public DbSet<ActionInFunction> ActionInFunctions { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    public DbSet<Product> Products { get; set; }


}
