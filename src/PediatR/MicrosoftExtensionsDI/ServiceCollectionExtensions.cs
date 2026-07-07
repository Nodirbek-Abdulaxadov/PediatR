namespace Microsoft.Extensions.DependencyInjection;

using System;
using PediatR.Registration;

/// <summary>
/// Extension methods for registering PediatR with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PediatR services and scans the configured assemblies for handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">A delegate that configures the registration.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPediatR(this IServiceCollection services, Action<PediatRServiceConfiguration> configuration)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var serviceConfiguration = new PediatRServiceConfiguration();
        configuration(serviceConfiguration);

        return services.AddPediatR(serviceConfiguration);
    }

    /// <summary>
    /// Registers PediatR services and scans the configured assemblies for handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The pre-built registration configuration.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPediatR(this IServiceCollection services, PediatRServiceConfiguration configuration)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (configuration.AssembliesToRegister.Count == 0)
        {
            throw new ArgumentException("No assemblies found to scan. Supply at least one assembly to scan for handlers.", nameof(configuration));
        }

        ServiceRegistrar.SetGenericRequestHandlerRegistrationLimitations(configuration);
        ServiceRegistrar.AddPediatRClassesWithTimeout(services, configuration);
        ServiceRegistrar.AddRequiredServices(services, configuration);

        return services;
    }
}
