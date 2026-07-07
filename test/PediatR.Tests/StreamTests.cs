namespace PediatR.Tests;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class StreamTests
{
    public class Counter : IStreamRequest<int>
    {
        public int Count { get; set; }
    }

    public class CounterHandler : IStreamRequestHandler<Counter, int>
    {
        public async IAsyncEnumerable<int> Handle(Counter request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var i = 0; i < request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return i;
            }
        }
    }

    public class DoublingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                yield return item;
                yield return item;
            }
        }
    }

    private static ServiceProvider BuildProvider(System.Action<PediatRServiceConfiguration>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<StreamTests>();
            extra?.Invoke(cfg);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateStream_yields_all_items_in_order()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new Counter { Count = 3 }))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 0, 1, 2 }, items);
    }

    [Fact]
    public async Task CreateStream_object_yields_boxed_items()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var items = new List<object?>();
        await foreach (var item in mediator.CreateStream((object)new Counter { Count = 2 }))
        {
            items.Add(item);
        }

        Assert.Equal(new object?[] { 0, 1 }, items);
    }

    [Fact]
    public async Task CreateStream_honors_cancellation()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new Counter { Count = 3 }, cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task CreateStream_applies_stream_behavior()
    {
        var mediator = BuildProvider(cfg => cfg.AddOpenStreamBehavior(typeof(DoublingStreamBehavior<,>)))
            .GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new Counter { Count = 2 }))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 0, 0, 1, 1 }, items);
    }
}
