namespace PediatR;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Sends a request through the mediator pipeline to be handled by a single handler.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Asynchronously sends a request to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the send operation, with the handler response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously sends a request with no response to a single handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the send operation.</returns>
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest;

    /// <summary>
    /// Asynchronously sends a request to a single handler via runtime typing.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the send operation, with the boxed handler response.</returns>
    Task<object?> Send(object request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream of responses from a single stream handler.
    /// </summary>
    /// <typeparam name="TResponse">The element type of the stream.</typeparam>
    /// <param name="request">The stream request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>An asynchronous sequence of responses.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream of responses from a single stream handler via runtime typing.
    /// </summary>
    /// <param name="request">The stream request object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>An asynchronous sequence of boxed responses.</returns>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
