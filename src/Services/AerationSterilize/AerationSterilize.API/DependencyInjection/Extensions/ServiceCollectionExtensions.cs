using AerationSterilize.API.Services;
using AerationSterilize.Application.Common;
using AerationSterilize.Application.DependencyInjection.Extensions;
using AerationSterilize.Persistence.Services;
using Contracts.Identity;
using Infrastructure.Extensions;
using Infrastructure.Identity;

namespace AerationSterilize.API.DependencyInjection.Extensions;

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

        services.AddTransient<ITokenService, TokenService>();
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
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddPermissionAuthorization();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

