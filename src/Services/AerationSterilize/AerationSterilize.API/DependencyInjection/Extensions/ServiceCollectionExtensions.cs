using AerationSterilize.API.DependencyInjection.Options;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

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
}

