namespace PediatR.Registration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PediatR.Pipeline;

/// <summary>
/// Performs the assembly scanning and service registration that backs <c>AddPediatR</c>. These
/// members are public to mirror the reference surface, but are intended for internal use.
/// </summary>
public static class ServiceRegistrar
{
    /// <summary>
    /// Validates the generic request handler registration limits declared on the configuration.
    /// </summary>
    public static void SetGenericRequestHandlerRegistrationLimitations(PediatRServiceConfiguration configuration)
    {
        if (configuration.MaxGenericTypeParameters < 0
            || configuration.MaxTypesClosing < 0
            || configuration.MaxGenericTypeRegistrations < 0
            || configuration.RegistrationTimeout < 0)
        {
            throw new InvalidOperationException("The generic handler registration limits must be non-negative.");
        }
    }

    /// <summary>
    /// Scans the configured assemblies and registers handlers, applying the configured registration timeout.
    /// </summary>
    public static void AddPediatRClassesWithTimeout(IServiceCollection services, PediatRServiceConfiguration configuration)
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(configuration.RegistrationTimeout));

        try
        {
            AddPediatRClasses(services, configuration, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The PediatR type registration process timed out.");
        }
    }

    /// <summary>
    /// Scans the configured assemblies and registers handlers.
    /// </summary>
    public static void AddPediatRClasses(IServiceCollection services, PediatRServiceConfiguration configuration)
        => AddPediatRClasses(services, configuration, CancellationToken.None);

    private static void AddPediatRClasses(IServiceCollection services, PediatRServiceConfiguration configuration, CancellationToken cancellationToken)
    {
        var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>), services, assembliesToScan, addIfAlreadyExists: false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<>), services, assembliesToScan, addIfAlreadyExists: false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>), services, assembliesToScan, addIfAlreadyExists: true, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>), services, assembliesToScan, addIfAlreadyExists: false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, addIfAlreadyExists: true, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>), services, assembliesToScan, addIfAlreadyExists: true, configuration, cancellationToken);

        if (configuration.AutoRegisterRequestProcessors)
        {
            // Route discovered processors into the configuration lists so the corresponding behaviors
            // are wired (and the processors actually run) in AddRequiredServices.
            ConnectImplementationsToTypesClosing(typeof(IRequestPreProcessor<>), services, assembliesToScan, addIfAlreadyExists: true, configuration, cancellationToken, configuration.RequestPreProcessorsToRegister);
            ConnectImplementationsToTypesClosing(typeof(IRequestPostProcessor<,>), services, assembliesToScan, addIfAlreadyExists: true, configuration, cancellationToken, configuration.RequestPostProcessorsToRegister);
        }
    }

    /// <summary>
    /// Registers the mediator, sender, publisher, notification publisher and the built-in pipeline
    /// behaviors. Must be called after <see cref="AddPediatRClasses(IServiceCollection, PediatRServiceConfiguration)"/>.
    /// </summary>
    public static void AddRequiredServices(IServiceCollection services, PediatRServiceConfiguration configuration)
    {
        var lifetime = configuration.Lifetime;

        services.TryAdd(new ServiceDescriptor(typeof(IMediator), configuration.MediatorImplementationType, lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), static sp => sp.GetRequiredService<IMediator>(), lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), static sp => sp.GetRequiredService<IMediator>(), lifetime));

        if (configuration.NotificationPublisherType is not null)
        {
            services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), configuration.NotificationPublisherType, lifetime));
        }
        else
        {
            services.TryAdd(new ServiceDescriptor(typeof(INotificationPublisher), configuration.NotificationPublisher));
        }

        // Built-in behaviors are registered before user behaviors, so they sit outermost in the
        // pipeline (first-registered = outermost). Exception actions wrap exception handlers for the
        // default ApplyForUnhandledExceptions strategy.
        if (configuration.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
        }
        else
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
        }

        if (configuration.RequestPreProcessorsToRegister.Count > 0)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(configuration.RequestPreProcessorsToRegister);
        }

        if (configuration.RequestPostProcessorsToRegister.Count > 0)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(configuration.RequestPostProcessorsToRegister);
        }

        foreach (var behavior in configuration.BehaviorsToRegister)
        {
            services.TryAddEnumerable(behavior);
        }

        foreach (var streamBehavior in configuration.StreamBehaviorsToRegister)
        {
            services.TryAddEnumerable(streamBehavior);
        }
    }

    private static void ConnectImplementationsToTypesClosing(
        Type openHandlerInterfaceType,
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        bool addIfAlreadyExists,
        PediatRServiceConfiguration configuration,
        CancellationToken cancellationToken,
        List<ServiceDescriptor>? sink = null)
    {
        var lifetime = configuration.Lifetime;

        foreach (var type in assemblies
                     .SelectMany(GetDefinedTypesSafe)
                     .Where(type => type.IsClass && !type.IsAbstract)
                     .Where(configuration.TypeEvaluator))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matchingInterfaces = type
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerInterfaceType)
                .ToList();

            if (matchingInterfaces.Count == 0)
            {
                continue;
            }

            if (type.IsGenericTypeDefinition)
            {
                // Only identity-closable open generics (e.g. Handler<T> : INotificationHandler<T>) are
                // supported for open registration; nested closings require RegisterGenericHandlers and
                // are intentionally not generated (documented limitation).
                if (!addIfAlreadyExists)
                {
                    continue;
                }

                foreach (var matchingInterface in matchingInterfaces)
                {
                    var interfaceArguments = matchingInterface.GetGenericArguments();
                    var isIdentityClosable = interfaceArguments.All(argument => argument.IsGenericParameter)
                        && interfaceArguments.Distinct().Count() == type.GetGenericArguments().Length;

                    if (!isIdentityClosable)
                    {
                        continue;
                    }

                    AddDescriptor(services, sink, new ServiceDescriptor(openHandlerInterfaceType, type, lifetime), addIfAlreadyExists);
                }

                continue;
            }

            foreach (var matchingInterface in matchingInterfaces)
            {
                AddDescriptor(services, sink, new ServiceDescriptor(matchingInterface, type, lifetime), addIfAlreadyExists);
            }
        }
    }

    private static void AddDescriptor(IServiceCollection services, List<ServiceDescriptor>? sink, ServiceDescriptor descriptor, bool addIfAlreadyExists)
    {
        if (sink is not null)
        {
            sink.Add(descriptor);
            return;
        }

        if (addIfAlreadyExists)
        {
            services.Add(descriptor);
        }
        else
        {
            services.TryAdd(descriptor);
        }
    }

    private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services, Type behaviorType, Type subBehaviorType)
    {
        var hasImplementations = services
            .Where(service => !service.IsKeyedService)
            .Select(service => service.ImplementationType)
            .OfType<Type>()
            .SelectMany(implementationType => implementationType.GetInterfaces())
            .Where(i => i.IsGenericType)
            .Select(i => i.GetGenericTypeDefinition())
            .Any(genericTypeDefinition => genericTypeDefinition == subBehaviorType);

        if (hasImplementations)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, ServiceLifetime.Transient));
        }
    }

    private static IEnumerable<Type> GetDefinedTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
