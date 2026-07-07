namespace PediatR.Wrappers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Non-generic base used by <see cref="Mediator"/> to resolve and dispatch the handlers for a
/// notification when only the runtime notification type is known.
/// </summary>
internal abstract class NotificationHandlerWrapper
{
    public abstract Task Handle(
        INotification notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the <see cref="INotificationHandler{TNotification}"/> instances for a notification and
/// hands them to the supplied publish strategy as <see cref="NotificationHandlerExecutor"/>s.
/// </summary>
internal sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override Task Handle(
        INotification notification,
        IServiceProvider serviceProvider,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        var handlers = serviceProvider
            .GetServices<INotificationHandler<TNotification>>()
            .Select(handler => new NotificationHandlerExecutor(
                handler,
                (theNotification, theToken) => handler.Handle((TNotification)theNotification, theToken)));

        return publish(handlers, notification, cancellationToken);
    }
}
