using CommonServices.Persistence.DependencyInjection.Options;
using CommonServices.Persistence.Repositories;
using Contracts.Common.Repositories;
using Infrastructure.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace CommonServices.Persistence.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddRepositoryBaseConfiguration(this IServiceCollection services)
    => services
        .AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork<ApplicationDbContext>))
        .AddScoped(typeof(IRepositoryBase<,>), typeof(ApplicationRepository<,>))
        ;

    public static void AddSqlConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>();
        if (databaseOptions == null || string.IsNullOrEmpty(databaseOptions.ConnectionString))
            throw new ArgumentNullException("Connection string is not configured.");

        services.AddDbContextPool<DbContext, ApplicationDbContext>((provider, builder) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var options = provider.GetRequiredService<IOptionsMonitor<SqlServerRetryOptions>>();

            builder
         .EnableDetailedErrors(true)
         .EnableSensitiveDataLogging(true)
         //   .UseLazyLoadingProxies(true) // => If UseLazyLoadingProxies, all of the navigation fields should be VIRTUAL
         .UseSqlServer(
                 connectionString: databaseOptions.ConnectionString,
                 sqlServerOptionsAction: optionsBuilder
                     => optionsBuilder
                     .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));


        });
    }

    public static OptionsBuilder<SqlServerRetryOptions> ConfigureSqlServerRetryOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlServerRetryOptions = configuration.GetSection(nameof(SqlServerRetryOptions));
        var result = services
              .AddOptions<SqlServerRetryOptions>()
              .Bind(sqlServerRetryOptions)
              .ValidateDataAnnotations()
              .ValidateOnStart();

        return result;
    }
}
