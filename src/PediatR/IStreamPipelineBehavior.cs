namespace PediatR;

using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Represents the continuation for the next action in the stream request pipeline.
/// </summary>
/// <typeparam name="TResponse">The element type of the stream.</typeparam>
/// <returns>An asynchronous sequence for the remainder of the pipeline.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();

/// <summary>
/// Defines a pipeline behavior that surrounds the handling of a streaming request.
/// </summary>
/// <typeparam name="TRequest">The stream request type.</typeparam>
/// <typeparam name="TResponse">The element type of the stream.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the stream request as part of the pipeline.
    /// </summary>
    /// <param name="request">The stream request object.</param>
    /// <param name="next">The continuation to run the rest of the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An asynchronous sequence of responses.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
