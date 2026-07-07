namespace PediatR;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PediatR.NotificationPublishers;
using PediatR.Wrappers;

/// <summary>
/// Default <see cref="IMediator"/> implementation. Resolves handlers, pipeline behaviors and
/// notification handlers from an <see cref="IServiceProvider"/>.
/// </summary>
public class Mediator : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlers = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlers = new();
    private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> _streamRequestHandlers = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class using the sequential
    /// <see cref="ForeachAwaitPublisher"/> notification strategy.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
    public Mediator(IServiceProvider serviceProvider)
        : this(serviceProvider, new ForeachAwaitPublisher())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
    /// <param name="publisher">The notification publish strategy.</param>
    public Mediator(IServiceProvider serviceProvider, INotificationPublisher publisher)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(request.GetType(), requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = (RequestHandlerWrapper)_requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = _requestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            Type wrapperType;

            var requestInterfaceType = requestType
                .GetInterfaces()
                .FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (requestInterfaceType is not null)
            {
                var responseType = requestInterfaceType.GetGenericArguments()[0];
                wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            }
            else if (typeof(IRequest).IsAssignableFrom(requestType))
            {
                wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
            }
            else
            {
                throw new ArgumentException($"{requestType.Name} does not implement IRequest or IRequest<TResponse>", nameof(request));
            }

            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        return PublishNotification(notification, cancellationToken);
    }

    /// <inheritdoc />
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        if (notification is INotification instance)
        {
            return PublishNotification(instance, cancellationToken);
        }

        throw new ArgumentException($"{notification.GetType().Name} does not implement INotification", nameof(notification));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = (StreamRequestHandlerWrapper<TResponse>)_streamRequestHandlers.GetOrAdd(request.GetType(), requestType =>
        {
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (StreamRequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = _streamRequestHandlers.GetOrAdd(request.GetType(), static requestType =>
        {
            var streamRequestInterfaceType = requestType
                .GetInterfaces()
                .FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));

            if (streamRequestInterfaceType is null)
            {
                throw new ArgumentException($"{requestType.Name} does not implement IStreamRequest<TResponse>", nameof(request));
            }

            var responseType = streamRequestInterfaceType.GetGenericArguments()[0];
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}");
            return (StreamRequestHandlerBase)wrapper;
        });

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Override in a derived class to control how notification handlers are invoked. The default
    /// implementation forwards to the configured <see cref="INotificationPublisher"/>.
    /// </summary>
    /// <param name="handlerExecutors">The resolved handler executors for the notification.</param>
    /// <param name="notification">The notification object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        => _publisher.Publish(handlerExecutors, notification, cancellationToken);

    private Task PublishNotification(INotification notification, CancellationToken cancellationToken)
    {
        var handler = _notificationHandlers.GetOrAdd(notification.GetType(), static notificationType =>
        {
            var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
            var wrapper = Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {notificationType}");
            return (NotificationHandlerWrapper)wrapper;
        });

        return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
    }
}
