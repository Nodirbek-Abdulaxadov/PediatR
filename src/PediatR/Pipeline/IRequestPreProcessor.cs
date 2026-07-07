namespace PediatR.Pipeline;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a pre-processor that runs before the handler for a request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestPreProcessor<in TRequest>
    where TRequest : notnull
{
    /// <summary>
    /// Processes the request before it reaches the handler.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the processing operation.</returns>
    Task Process(TRequest request, CancellationToken cancellationToken);
}
