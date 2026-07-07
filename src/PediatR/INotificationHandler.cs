namespace PediatR;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a handler for a notification.
/// </summary>
/// <typeparam name="TNotification">The type of notification handled.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The notification object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the handling operation.</returns>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Convenience base class for notification handlers that run synchronously.
/// Override <see cref="Handle(TNotification)"/> to implement the handler.
/// </summary>
/// <typeparam name="TNotification">The type of notification handled.</typeparam>
public abstract class NotificationHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    Task INotificationHandler<TNotification>.Handle(TNotification notification, CancellationToken cancellationToken)
    {
        Handle(notification);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the notification synchronously.
    /// </summary>
    /// <param name="notification">The notification object.</param>
    protected abstract void Handle(TNotification notification);
}
