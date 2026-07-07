namespace Benchmarks.PediatrLib;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PediatR;

// Types implemented against the PediatR interfaces (identical shape to MediatrScenarios).

public sealed class PPing : IRequest<string>
{
}

public sealed class PPingHandler : IRequestHandler<PPing, string>
{
    public Task<string> Handle(PPing request, CancellationToken cancellationToken) => Task.FromResult("pong");
}

public sealed class PNotify : INotification
{
}

public sealed class PNotifyHandlerOne : INotificationHandler<PNotify>
{
    public Task Handle(PNotify notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class PNotifyHandlerTwo : INotificationHandler<PNotify>
{
    public Task Handle(PNotify notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class PStream : IStreamRequest<int>
{
    public int Count { get; set; } = 10;
}

public sealed class PStreamHandler : IStreamRequestHandler<PStream, int>
{
    public async IAsyncEnumerable<int> Handle(PStream request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        for (var i = 0; i < request.Count; i++)
        {
            yield return i;
        }
    }
}

public sealed class PBehaviorOne<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}

public sealed class PBehaviorTwo<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}

public sealed class PBehaviorThree<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next();
}
