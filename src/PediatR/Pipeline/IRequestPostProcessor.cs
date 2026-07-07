namespace PediatR.Pipeline;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a post-processor that runs after the handler for a request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Processes the request after the handler has produced a response.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="response">The response produced by the handler.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the processing operation.</returns>
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
