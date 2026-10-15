using Contracts.Caching;
using Contracts.Identity;
using Infrastructure.Caching;
using Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Options;
using StackExchange.Redis;

namespace Infrastructure.Extensions;
public static class CachingServiceCollectionExtensions
{
    // Registers the Redis cache shared by all services.
    // In the identity service (AerationSterilize) it must be called after IPermissionService has been registered,
    // because it decorates that registration. Services without IPermissionService (they only read the token) skip it.
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(nameof(RedisOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var redisOptions = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            configurationOptions.AbortOnConnectFail = false; // keep retrying in the background instead of crashing on startup

            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ITokenRevocationService, RedisTokenRevocationService>();
        if (services.Any(d => d.ServiceType == typeof(IPermissionService)))
            services.Decorate<IPermissionService, CachedPermissionService>();

        return services;
    }

    private static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"{typeof(TService).Name} must be registered before it can be decorated.");

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            provider => ActivatorUtilities.CreateInstance<TDecorator>(provider, CreateInner(provider, descriptor)),
            descriptor.Lifetime));

        return services;
    }

    private static object CreateInner(IServiceProvider provider, ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
           ?? descriptor.ImplementationFactory?.Invoke(provider)
           ?? ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType!);
}
