namespace PediatR;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Pairs a resolved notification handler instance with a callback that invokes it. Used by
/// <see cref="INotificationPublisher"/> implementations to control how handlers are dispatched.
/// </summary>
/// <param name="HandlerInstance">The resolved handler instance.</param>
/// <param name="HandlerCallback">A callback that invokes the handler for a notification.</param>
public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback);
