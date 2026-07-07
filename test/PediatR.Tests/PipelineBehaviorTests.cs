namespace PediatR.Tests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PediatR.Pipeline;
using Xunit;

public class PipelineBehaviorTests
{
    public class OrderPing : IRequest<string>
    {
    }

    public class OrderPingHandler : IRequestHandler<OrderPing, string>
    {
        private readonly Recorder _recorder;

        public OrderPingHandler(Recorder recorder) => _recorder = recorder;

        public Task<string> Handle(OrderPing request, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("Handler");
            return Task.FromResult("ok");
        }
    }

    public class FirstBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Recorder _recorder;

        public FirstBehavior(Recorder recorder) => _recorder = recorder;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("First:Before");
            var response = await next(cancellationToken);
            _recorder.Messages.Add("First:After");
            return response;
        }
    }

    public class SecondBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Recorder _recorder;

        public SecondBehavior(Recorder recorder) => _recorder = recorder;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("Second:Before");
            var response = await next(cancellationToken);
            _recorder.Messages.Add("Second:After");
            return response;
        }
    }

    public class RecordingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
        where TRequest : notnull
    {
        private readonly Recorder _recorder;

        public RecordingPreProcessor(Recorder recorder) => _recorder = recorder;

        public Task Process(TRequest request, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("Pre");
            return Task.CompletedTask;
        }
    }

    public class RecordingPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Recorder _recorder;

        public RecordingPostProcessor(Recorder recorder) => _recorder = recorder;

        public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("Post");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Behaviors_nest_first_registered_outermost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineBehaviorTests>();
            cfg.AddOpenBehavior(typeof(FirstBehavior<,>));
            cfg.AddOpenBehavior(typeof(SecondBehavior<,>));
        });
        var provider = services.BuildServiceProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IMediator>().Send(new OrderPing());

        Assert.Equal(
            new[] { "First:Before", "Second:Before", "Handler", "Second:After", "First:After" },
            recorder.Messages);
    }

    [Fact]
    public async Task Pre_and_post_processors_wrap_user_behaviors_and_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PipelineBehaviorTests>();
            cfg.AddOpenRequestPreProcessor(typeof(RecordingPreProcessor<>));
            cfg.AddOpenRequestPostProcessor(typeof(RecordingPostProcessor<,>));
            cfg.AddOpenBehavior(typeof(FirstBehavior<,>));
            cfg.AddOpenBehavior(typeof(SecondBehavior<,>));
        });
        var provider = services.BuildServiceProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IMediator>().Send(new OrderPing());

        Assert.Equal(
            new[] { "Pre", "First:Before", "Second:Before", "Handler", "Second:After", "First:After", "Post" },
            recorder.Messages);
    }
}
