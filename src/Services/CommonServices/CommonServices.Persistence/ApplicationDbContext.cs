using CommonServices.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace CommonServices.Persistence;

// Users, roles and permissions live in AerationSterilize (identity service), not here.
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
        => builder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

    public DbSet<Language> Languages { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<Bank> Banks { get; set; }
}
