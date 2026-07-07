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
        Task<TResponse> Handler(CancellationToken t = default)
            => serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .Handle((TRequest)request, t == default ? cancellationToken : t);

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                (RequestHandlerDelegate<TResponse>)Handler,
                (next, pipeline) => (RequestHandlerDelegate<TResponse>)((t) => pipeline.Handle((TRequest)request, next, t == default ? cancellationToken : t)))(cancellationToken);
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
        async Task<Unit> Handler(CancellationToken t = default)
        {
            await serviceProvider.GetRequiredService<IRequestHandler<TRequest>>()
                .Handle((TRequest)request, t == default ? cancellationToken : t)
                .ConfigureAwait(false);
            return Unit.Value;
        }

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, Unit>>()
            .Reverse()
            .Aggregate(
                (RequestHandlerDelegate<Unit>)Handler,
                (next, pipeline) => (RequestHandlerDelegate<Unit>)((t) => pipeline.Handle((TRequest)request, next, t == default ? cancellationToken : t)))(cancellationToken);
    }
}
