namespace PediatR.Wrappers;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Non-generic base used by <see cref="Mediator"/> to invoke a request handler when only the
/// runtime request type is known (the <c>Send(object)</c> entry point).
/// </summary>
internal abstract class RequestHandlerBase
{
    public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Base for the <see cref="IRequest{TResponse}"/> branch, exposing a strongly typed entry point.
/// </summary>
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the handler for a request that returns a response and composes the pipeline of
/// <see cref="IPipelineBehavior{TRequest, TResponse}"/> instances around it.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        Task<TResponse> Handler(CancellationToken t = default)
            => serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .Handle(typedRequest, t == default ? cancellationToken : t);

        var next = (RequestHandlerDelegate<TResponse>)Handler;

        // Fold the behaviors so the first-registered runs outermost. The common no-behavior case
        // short-circuits straight to the handler with no LINQ buffer or accumulator closures. The
        // service provider returns the behaviors as an array, so we iterate it in reverse in place.
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        if (behaviors is IPipelineBehavior<TRequest, TResponse>[] array)
        {
            for (var i = array.Length - 1; i >= 0; i--)
            {
                var behavior = array[i];
                var inner = next;
                next = t => behavior.Handle(typedRequest, inner, t == default ? cancellationToken : t);
            }
        }
        else
        {
            foreach (var behavior in behaviors.Reverse())
            {
                var current = behavior;
                var inner = next;
                next = t => current.Handle(typedRequest, inner, t == default ? cancellationToken : t);
            }
        }

        return next(cancellationToken);
    }
}

/// <summary>
/// Base for the void <see cref="IRequest"/> branch, whose handler returns <see cref="Unit"/>.
/// </summary>
internal abstract class RequestHandlerWrapper : RequestHandlerBase
{
    public abstract Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the handler for a request that returns no value and composes the pipeline of
/// <see cref="IPipelineBehavior{TRequest, Unit}"/> instances around it.
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapper
    where TRequest : IRequest
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => await Handle((IRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;

        async Task<Unit> Handler(CancellationToken t = default)
        {
            await serviceProvider.GetRequiredService<IRequestHandler<TRequest>>()
                .Handle(typedRequest, t == default ? cancellationToken : t)
                .ConfigureAwait(false);
            return Unit.Value;
        }

        var next = (RequestHandlerDelegate<Unit>)Handler;

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, Unit>>();
        if (behaviors is IPipelineBehavior<TRequest, Unit>[] array)
        {
            for (var i = array.Length - 1; i >= 0; i--)
            {
                var behavior = array[i];
                var inner = next;
                next = t => behavior.Handle(typedRequest, inner, t == default ? cancellationToken : t);
            }
        }
        else
        {
            foreach (var behavior in behaviors.Reverse())
            {
                var current = behavior;
                var inner = next;
                next = t => current.Handle(typedRequest, inner, t == default ? cancellationToken : t);
            }
        }

        return next(cancellationToken);
    }
}
