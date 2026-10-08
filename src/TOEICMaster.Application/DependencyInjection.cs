using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Common.Behaviors;

namespace TOEICMaster.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        string? mediatorLicenseKey = null, string? mapperLicenseKey = null,
        params Assembly[] additionalMappingAssemblies)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            if (!string.IsNullOrWhiteSpace(mediatorLicenseKey))
                configuration.LicenseKey = mediatorLicenseKey;
        });
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);
        services.AddAutoMapper(configuration =>
        {
            if (!string.IsNullOrWhiteSpace(mapperLicenseKey))
                configuration.LicenseKey = mapperLicenseKey;
        }, additionalMappingAssemblies.Prepend(assembly).Distinct().ToArray());
        return services;
    }
}
