namespace PediatR;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents the continuation for the next action in the request pipeline. Invoke it to
/// run the inner behavior (or the handler, if this is the innermost behavior).
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="t">An optional cancellation token flowed to the remainder of the pipeline.</param>
/// <returns>A task representing the remainder of the pipeline, with the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken t = default);

/// <summary>
/// Defines a pipeline behavior that surrounds the handling of a request. Behaviors run in
/// registration order, with the first-registered behavior being the outermost.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the request as part of the pipeline.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="next">The continuation to run the rest of the pipeline.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the handling operation, with the response.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
