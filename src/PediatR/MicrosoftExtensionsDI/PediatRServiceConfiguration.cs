namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PediatR;
using PediatR.Entities;
using PediatR.NotificationPublishers;
using PediatR.Pipeline;

/// <summary>
/// Configures how PediatR discovers and registers handlers, behaviors, processors and the
/// notification publish strategy.
/// </summary>
public class PediatRServiceConfiguration
{
    /// <summary>
    /// Gets or sets a predicate used to decide which scanned types are considered for registration.
    /// </summary>
    public Func<Type, bool> TypeEvaluator { get; set; } = _ => true;

    /// <summary>
    /// Gets or sets the concrete <see cref="IMediator"/> implementation type. Defaults to <see cref="Mediator"/>.
    /// </summary>
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);

    /// <summary>
    /// Gets or sets the notification publisher instance used when <see cref="NotificationPublisherType"/> is not set.
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; set; } = new ForeachAwaitPublisher();

    /// <summary>
    /// Gets or sets the notification publisher type. When set, it is resolved from the container
    /// with <see cref="Lifetime"/> instead of using <see cref="NotificationPublisher"/>.
    /// </summary>
    public Type? NotificationPublisherType { get; set; }

    /// <summary>
    /// Gets or sets the lifetime used for the mediator, sender, publisher, notification publisher and
    /// discovered handlers. Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Gets or sets the strategy controlling when exception actions run. Defaults to
    /// <see cref="RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions"/>.
    /// </summary>
    public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
        = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    internal List<Assembly> AssembliesToRegister { get; } = new();

    /// <summary>
    /// Gets the list of pipeline behavior registrations accumulated by the various <c>AddBehavior</c> methods.
    /// </summary>
    public List<ServiceDescriptor> BehaviorsToRegister { get; } = new();

    /// <summary>
    /// Gets the list of stream pipeline behavior registrations accumulated by the <c>AddStreamBehavior</c> methods.
    /// </summary>
    public List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = new();

    /// <summary>
    /// Gets the list of pre-processor registrations accumulated by the <c>AddRequestPreProcessor</c> methods.
    /// </summary>
    public List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = new();

    /// <summary>
    /// Gets the list of post-processor registrations accumulated by the <c>AddRequestPostProcessor</c> methods.
    /// </summary>
    public List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether pre/post processors discovered while scanning are
    /// automatically registered and wired into the pipeline.
    /// </summary>
    public bool AutoRegisterRequestProcessors { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of generic type parameters a generic request handler may declare
    /// before registration is aborted. Defaults to 10.
    /// </summary>
    public int MaxGenericTypeParameters { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of types that may close a single generic parameter. Defaults to 100.
    /// </summary>
    public int MaxTypesClosing { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum total number of generic handler registrations allowed. Defaults to 125000.
    /// </summary>
    public int MaxGenericTypeRegistrations { get; set; } = 125000;

    /// <summary>
    /// Gets or sets the registration timeout in milliseconds for the scanning phase. Defaults to 15000.
    /// </summary>
    public int RegistrationTimeout { get; set; } = 15000;

    /// <summary>
    /// Gets or sets a value indicating whether closed registrations for generic request handlers are generated.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool RegisterGenericHandlers { get; set; }

    /// <summary>
    /// Registers services from the assembly containing <typeparamref name="T"/>.
    /// </summary>
    public PediatRServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Registers services from the assembly containing <paramref name="type"/>.
    /// </summary>
    public PediatRServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    /// <summary>
    /// Registers services from the given assembly.
    /// </summary>
    public PediatRServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers services from the given assemblies.
    /// </summary>
    public PediatRServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Registers a pipeline behavior mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddBehavior<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a pipeline behavior implementation against every <see cref="IPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddBehavior<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a pipeline behavior implementation against every <see cref="IPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddBehavior(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddInterfaces(BehaviorsToRegister, implementationType, typeof(IPipelineBehavior<,>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers a pipeline behavior mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddBehavior(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior for all requests.
    /// </summary>
    public PediatRServiceConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddOpen(BehaviorsToRegister, openBehaviorType, typeof(IPipelineBehavior<,>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers multiple open-generic pipeline behaviors.
    /// </summary>
    public PediatRServiceConfiguration AddOpenBehaviors(IEnumerable<Type> openBehaviorTypes, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        foreach (var openBehaviorType in openBehaviorTypes)
        {
            AddOpenBehavior(openBehaviorType, serviceLifetime);
        }

        return this;
    }

    /// <summary>
    /// Registers multiple open-generic pipeline behaviors described by <see cref="OpenBehavior"/> entries.
    /// </summary>
    public PediatRServiceConfiguration AddOpenBehaviors(IEnumerable<OpenBehavior> openBehaviors)
    {
        foreach (var openBehavior in openBehaviors)
        {
            AddOpenBehavior(openBehavior.OpenBehaviorType, openBehavior.ServiceLifetime);
        }

        return this;
    }

    /// <summary>
    /// Registers a stream pipeline behavior mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddStreamBehavior<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a stream pipeline behavior mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddStreamBehavior(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        StreamBehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers a stream pipeline behavior implementation against every <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddStreamBehavior<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a stream pipeline behavior implementation against every <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddStreamBehavior(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddInterfaces(StreamBehaviorsToRegister, implementationType, typeof(IStreamPipelineBehavior<,>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers an open-generic stream pipeline behavior for all stream requests.
    /// </summary>
    public PediatRServiceConfiguration AddOpenStreamBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddOpen(StreamBehaviorsToRegister, openBehaviorType, typeof(IStreamPipelineBehavior<,>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers a pre-processor mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPreProcessor<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a pre-processor mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPreProcessor(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPreProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers a pre-processor implementation against every <see cref="IRequestPreProcessor{TRequest}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPreProcessor<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a pre-processor implementation against every <see cref="IRequestPreProcessor{TRequest}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPreProcessor(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddInterfaces(RequestPreProcessorsToRegister, implementationType, typeof(IRequestPreProcessor<>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers an open-generic pre-processor for all requests.
    /// </summary>
    public PediatRServiceConfiguration AddOpenRequestPreProcessor(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddOpen(RequestPreProcessorsToRegister, openBehaviorType, typeof(IRequestPreProcessor<>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers a post-processor mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPostProcessor<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a post-processor mapping the given service type to the given implementation type.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPostProcessor(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPostProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
        return this;
    }

    /// <summary>
    /// Registers a post-processor implementation against every <see cref="IRequestPostProcessor{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPostProcessor<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Registers a post-processor implementation against every <see cref="IRequestPostProcessor{TRequest, TResponse}"/> it implements.
    /// </summary>
    public PediatRServiceConfiguration AddRequestPostProcessor(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddInterfaces(RequestPostProcessorsToRegister, implementationType, typeof(IRequestPostProcessor<,>), serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers an open-generic post-processor for all requests.
    /// </summary>
    public PediatRServiceConfiguration AddOpenRequestPostProcessor(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        AddOpen(RequestPostProcessorsToRegister, openBehaviorType, typeof(IRequestPostProcessor<,>), serviceLifetime);
        return this;
    }

    private static void AddInterfaces(List<ServiceDescriptor> list, Type implementationType, Type openInterfaceType, ServiceLifetime serviceLifetime)
    {
        var implementedInterfaces = implementationType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterfaceType)
            .ToList();

        if (implementedInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {openInterfaceType.FullName}");
        }

        foreach (var implementedInterface in implementedInterfaces)
        {
            list.Add(new ServiceDescriptor(implementedInterface, implementationType, serviceLifetime));
        }
    }

    private static void AddOpen(List<ServiceDescriptor> list, Type openType, Type openInterfaceType, ServiceLifetime serviceLifetime)
    {
        if (!openType.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException($"{openType.Name} must be an open generic type definition");
        }

        var implementsInterface = openType
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterfaceType);

        if (!implementsInterface)
        {
            throw new InvalidOperationException($"{openType.Name} must implement {openInterfaceType.FullName}");
        }

        list.Add(new ServiceDescriptor(openInterfaceType, openType, serviceLifetime));
    }
}
