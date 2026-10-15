using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommonServices.Persistence.DependencyInjection.Extensions;
public static class ApplicationBuilderExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<Serilog.ILogger>();
        var seeder = new ApplicationContextSeed(context, logger);

        await seeder.InitializeAsync();
        await seeder.SeedAsync();
    }
}
