using AerationSterilize.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AerationSterilize.Application.DependencyInjection.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfigureMediaR(this IServiceCollection services)
    {
        services
            .AddConfigureAutoMapper()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly))
            .AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>))
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
