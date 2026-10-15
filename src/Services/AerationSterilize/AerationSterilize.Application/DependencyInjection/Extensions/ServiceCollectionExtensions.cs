using AerationSterilize.Application.Behaviors;
using Infrastructure.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
}
