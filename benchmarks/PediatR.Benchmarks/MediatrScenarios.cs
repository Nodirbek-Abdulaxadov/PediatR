namespace Benchmarks.MediatrLib;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

// Types implemented against the real MediatR interfaces.

public sealed class MPing : IRequest<string>
{
}

public sealed class MPingHandler : IRequestHandler<MPing, string>
{
    public Task<string> Handle(MPing request, CancellationToken cancellationToken) => Task.FromResult("pong");
}

public sealed class MNotify : INotification
{
}

public sealed class MNotifyHandlerOne : INotificationHandler<MNotify>
{
    public Task Handle(MNotify notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class MNotifyHandlerTwo : INotificationHandler<MNotify>
{
    public Task Handle(MNotify notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class MStream : IStreamRequest<int>
{
    public int Count { get; set; } = 10;
}

public sealed class MStreamHandler : IStreamRequestHandler<MStream, int>
{
    public async IAsyncEnumerable<int> Handle(MStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        for (var i = 0; i < request.Count; i++)
        {
            yield return i;
        }
    }
}

public sealed class MBehaviorOne<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}

public sealed class MBehaviorTwo<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}

public sealed class MBehaviorThree<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}
