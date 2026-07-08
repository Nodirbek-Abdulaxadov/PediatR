namespace PediatR;

/// <summary>
/// Marker interface for a read-only request (a <em>query</em>) that returns a response of type
/// <typeparamref name="TResponse"/>. Extends <see cref="IRequest{TResponse}"/>, so queries flow through
/// the same mediator pipeline as any other request.
/// </summary>
/// <remarks>
/// The distinct marker exists purely to enable query-targeted pipeline behaviors, e.g. a caching
/// behavior constrained with <c>where TRequest : IQuery&lt;TResponse&gt;</c>. It is additive and does not
/// affect MediatR source compatibility: hand-written or find-and-replaced code that only uses
/// <see cref="IRequest{TResponse}"/> is unaffected.
/// </remarks>
/// <typeparam name="TResponse">The type of response produced for this query.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{
}
