namespace PediatR.Pipeline;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines a side-effecting action to run when an exception of type <typeparamref name="TException"/>
/// is thrown while handling a request. Unlike <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>,
/// an action cannot recover from the exception; the exception is always re-thrown afterwards.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TException">The exception type.</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Executes the action for the thrown exception.
    /// </summary>
    /// <param name="request">The request that was being handled.</param>
    /// <param name="exception">the exception that was thrown.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the action.</returns>
    Task Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
