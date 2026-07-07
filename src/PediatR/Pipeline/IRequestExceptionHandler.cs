namespace PediatR.Pipeline;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a handler that can recover from an exception of type <typeparamref name="TException"/>
/// thrown while handling a request. Call <see cref="RequestExceptionHandlerState{TResponse}.SetHandled"/>
/// to mark the exception as handled and provide a response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <typeparam name="TException">The exception type handled.</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Handles the exception.
    /// </summary>
    /// <param name="request">The request that was being handled.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="state">The state used to signal that the exception has been handled.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the handling operation.</returns>
    Task Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}
