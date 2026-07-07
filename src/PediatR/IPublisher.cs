namespace PediatR;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Publishes a notification through the mediator to zero or more handlers.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Asynchronously publishes a notification to its handlers via runtime typing.
    /// </summary>
    /// <param name="notification">The notification object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task Publish(object notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously publishes a notification to its handlers.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
