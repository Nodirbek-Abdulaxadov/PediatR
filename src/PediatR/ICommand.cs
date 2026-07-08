namespace PediatR;

/// <summary>
/// Marker interface for a state-changing request (a <em>command</em>) that returns a response of
/// type <typeparamref name="TResponse"/>. Extends <see cref="IRequest{TResponse}"/>, so commands flow
/// through the same mediator pipeline as any other request.
/// </summary>
/// <remarks>
/// The distinct marker exists purely to enable command-targeted pipeline behaviors, e.g.
/// <c>class TxBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt; where TRequest : ICommand&lt;TResponse&gt;</c>.
/// It is additive and does not affect MediatR source compatibility: hand-written or find-and-replaced
/// code that only uses <see cref="IRequest{TResponse}"/> is unaffected.
/// </remarks>
/// <typeparam name="TResponse">The type of response produced for this command.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
