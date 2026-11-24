using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TOEICMaster.Application.Common.Behaviors;

namespace TOEICMaster.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        string? mediatorLicenseKey = null)
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
        return services;
    }
}
