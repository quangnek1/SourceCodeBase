using CommonServices.API.Authorization;
using CommonServices.API.Services;
using CommonServices.Application.Common;
using CommonServices.Application.DependencyInjection.Extensions;
using CommonServices.Persistence.Services;
using Contracts.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

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

        services.AddTransient<ITokenService, TokenService>();
        services.AddConfigureJWTAuthentication(configuration);
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
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

}

