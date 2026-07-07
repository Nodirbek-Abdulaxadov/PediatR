namespace PediatR;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a handler for a request that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request handled.</typeparam>
/// <typeparam name="TResponse">The type of response produced.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the handling operation, with the response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a handler for a request that does not return a value.
/// </summary>
/// <typeparam name="TRequest">The type of request handled.</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request object.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the handling operation.</returns>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}
