namespace PediatR;

using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Defines a handler for a streaming request that yields a sequence of responses.
/// </summary>
/// <typeparam name="TRequest">The type of stream request handled.</typeparam>
/// <typeparam name="TResponse">The type of the elements produced.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request.
    /// </summary>
    /// <param name="request">The stream request object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An asynchronous sequence of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
