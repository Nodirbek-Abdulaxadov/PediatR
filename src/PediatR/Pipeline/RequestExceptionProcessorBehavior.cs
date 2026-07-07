namespace PediatR.Pipeline;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pipeline behavior that catches exceptions thrown by the rest of the pipeline and offers them
/// to the registered <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
/// instances, from the most-derived exception type to the least. If a handler marks the exception
/// as handled, its response is returned; otherwise the exception is re-thrown.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class RequestExceptionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestExceptionProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve exception handlers.</param>
    public RequestExceptionProcessorBehavior(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var state = new RequestExceptionHandlerState<TResponse>();

            foreach (var exceptionType in ExceptionTypeHierarchy(exception.GetType()))
            {
                var handlerType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);
                var handleMethod = handlerType.GetMethod(nameof(IRequestExceptionHandler<TRequest, TResponse, Exception>.Handle))!;

                foreach (var handler in _serviceProvider.GetServices(handlerType))
                {
                    if (handler is null)
                    {
                        continue;
                    }

                    await ((Task)handleMethod.Invoke(handler, new object[] { request, exception, state, cancellationToken })!).ConfigureAwait(false);

                    if (state.Handled)
                    {
                        break;
                    }
                }

                if (state.Handled)
                {
                    break;
                }
            }

            if (!state.Handled)
            {
                throw;
            }

            return state.Response!;
        }
    }

    private static IEnumerable<Type> ExceptionTypeHierarchy(Type exceptionType)
    {
        var current = exceptionType;
        while (current is not null && current != typeof(object))
        {
            yield return current;
            current = current.BaseType;
        }
    }
}
