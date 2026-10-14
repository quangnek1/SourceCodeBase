using System.Text;
using AerationSterilize.Application.Behaviors;
using Infrastructure.Behaviors;
using Contracts.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Options;

namespace AerationSterilize.Application.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static void AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        services.AddSingleton(jwtOptions);
    }
    public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
    {
        services
            .AddConfigureAutoMapper()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly))
            .AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>))
            ;
        return services;
    }

    public static IServiceCollection AddConfigureAutoMapper(this IServiceCollection services)
    {
        services
            .AddAutoMapper(_ => { }, AssemblyReference.Assembly)
            ;
        return services;
    }

    public static IServiceCollection AddConfigureJWTAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();
        if (jwtOptions == null || string.IsNullOrEmpty(jwtOptions.SecretKey))
        {
            throw new ArgumentNullException($"{nameof(JwtOptions)} is not configured properly");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ClockSkew = TimeSpan.Zero
        };
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x =>
        {
            x.SaveToken = true;
            x.RequireHttpsMetadata = false;
            x.TokenValidationParameters = tokenValidationParameters;
            x.Events = new JwtBearerEvents
            {
                // Reject access tokens that were revoked on logout, even though their signature and lifetime are still valid.
                OnTokenValidated = async context =>
                {
                    var jti = context.SecurityToken?.Id;
                    if (string.IsNullOrEmpty(jti))
                    {
                        context.Fail("Token has no jti.");
                        return;
                    }

                    var tokenRevocationService = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationService>();
                    if (await tokenRevocationService.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                        context.Fail("Token has been revoked.");
                }
            };
        });

        return services;
    }
}
