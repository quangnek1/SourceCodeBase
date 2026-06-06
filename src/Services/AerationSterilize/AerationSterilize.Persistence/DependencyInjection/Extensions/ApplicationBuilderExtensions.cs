using AerationSterilize.Domain.Entities.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AerationSterilize.Persistence.DependencyInjection.Extensions;
public static class ApplicationBuilderExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        var logger = services.GetRequiredService<Serilog.ILogger>();
        var seeder = new ApplicationContextSeed(context, userManager, roleManager, logger);

        await seeder.InitializeAsync();
        await seeder.SeedAsync();
    }
}
