namespace PediatR;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines the strategy used to dispatch a notification to its handlers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to the supplied handler executors.
    /// </summary>
    /// <param name="handlerExecutors">The resolved handler executors for the notification.</param>
    /// <param name="notification">The notification object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken);
}
