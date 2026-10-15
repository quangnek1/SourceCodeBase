using System.Text;
using Contracts.Identity;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Options;

namespace Infrastructure.Extensions;
public static class AuthenticationServiceCollectionExtensions
{
    // Validates access tokens issued by AerationSterilize (the identity service).
    // Every service must use the same JwtOptions (SecretKey, Issuer, Audience) as AerationSterilize.
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
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

    // [Permission(FunctionCode, CommandCode)] is checked against the permission claims inside the access token.
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        services.AddHttpContextAccessor();

        return services;
    }
}
