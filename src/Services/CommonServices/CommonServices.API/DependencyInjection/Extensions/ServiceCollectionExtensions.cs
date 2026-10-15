using CommonServices.API.Services;
using CommonServices.Application.Common;
using Infrastructure.Extensions;

namespace CommonServices.API.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApiVersioningConfiguration(this IServiceCollection services)
       => services.AddApiVersioning(options => options.ReportApiVersions = true)
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

    public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration["AllowOrigins"];

        // Login / token issuing lives in AerationSterilize; this service only validates its access tokens.
        services.AddJwtAuthentication(configuration);
        services.AddCors(option =>
        {
            option.AddPolicy("CorsPolicy", buider =>
            {
                buider.WithOrigins(origins: origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
        });
    }

    public static void AddAuthorizationConfigurationAPI(this IServiceCollection services)
    {
        // Permissions are read from the claims of the AerationSterilize access token.
        services.AddPermissionAuthorization();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

