namespace PediatR.Wrappers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Non-generic base used by <see cref="Mediator"/> to invoke a stream handler when only the
/// runtime request type is known (the <c>CreateStream(object)</c> entry point).
/// </summary>
internal abstract class StreamRequestHandlerBase
{
    public abstract IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Base for the strongly typed stream entry point.
/// </summary>
internal abstract class StreamRequestHandlerWrapper<TResponse> : StreamRequestHandlerBase
{
    public abstract IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the stream handler for a request and composes the pipeline of
/// <see cref="IStreamPipelineBehavior{TRequest, TResponse}"/> instances around it.
/// </summary>
internal sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse> : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override async IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = Handle((IStreamRequest<TResponse>)request, serviceProvider, cancellationToken);

        await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    public override IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        StreamHandlerDelegate<TResponse> seed = () =>
            serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>()
                .Handle((TRequest)request, cancellationToken);

        return serviceProvider
            .GetServices<IStreamPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                seed,
                (next, pipeline) => (StreamHandlerDelegate<TResponse>)(() => pipeline.Handle((TRequest)request, next, cancellationToken)))();
    }
}
