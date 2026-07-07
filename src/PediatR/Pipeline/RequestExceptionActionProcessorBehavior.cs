namespace PediatR.Pipeline;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pipeline behavior that runs the registered <see cref="IRequestExceptionAction{TRequest, TException}"/>
/// instances when the rest of the pipeline throws, then re-throws the original exception. Actions run
/// from the most-derived exception type to the least.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestExceptionActionProcessorBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve exception actions.</param>
    public RequestExceptionActionProcessorBehavior(IServiceProvider serviceProvider)
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
            foreach (var exceptionType in ExceptionTypeHierarchy(exception.GetType()))
            {
                var actionType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
                var executeMethod = actionType.GetMethod(nameof(IRequestExceptionAction<TRequest, Exception>.Execute))!;

                foreach (var action in _serviceProvider.GetServices(actionType))
                {
                    if (action is null)
                    {
                        continue;
                    }

                    await ((Task)executeMethod.Invoke(action, new object[] { request, exception, cancellationToken })!).ConfigureAwait(false);
                }
            }

            throw;
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
