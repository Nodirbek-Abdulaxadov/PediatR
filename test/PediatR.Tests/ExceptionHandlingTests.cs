namespace PediatR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PediatR.Pipeline;
using Xunit;

public class ExceptionHandlingTests
{
    public class HandledRequest : IRequest<string>
    {
    }

    public class HandledRequestHandler : IRequestHandler<HandledRequest, string>
    {
        public Task<string> Handle(HandledRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    public class HandledRequestExceptionHandler : IRequestExceptionHandler<HandledRequest, string, InvalidOperationException>
    {
        public Task Handle(HandledRequest request, InvalidOperationException exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
        {
            state.SetHandled("recovered");
            return Task.CompletedTask;
        }
    }

    public class ActionRequest : IRequest<string>
    {
    }

    public class ActionRequestHandler : IRequestHandler<ActionRequest, string>
    {
        public Task<string> Handle(ActionRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    public class ActionRequestExceptionAction : IRequestExceptionAction<ActionRequest, Exception>
    {
        private readonly Recorder _recorder;

        public ActionRequestExceptionAction(Recorder recorder) => _recorder = recorder;

        public Task Execute(ActionRequest request, Exception exception, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("action");
            return Task.CompletedTask;
        }
    }

    public class BothRequest : IRequest<string>
    {
    }

    public class BothRequestHandler : IRequestHandler<BothRequest, string>
    {
        public Task<string> Handle(BothRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    public class BothRequestExceptionHandler : IRequestExceptionHandler<BothRequest, string, InvalidOperationException>
    {
        public Task Handle(BothRequest request, InvalidOperationException exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
        {
            state.SetHandled("recovered");
            return Task.CompletedTask;
        }
    }

    public class BothRequestExceptionAction : IRequestExceptionAction<BothRequest, Exception>
    {
        private readonly Recorder _recorder;

        public BothRequestExceptionAction(Recorder recorder) => _recorder = recorder;

        public Task Execute(BothRequest request, Exception exception, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("action");
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider(System.Action<PediatRServiceConfiguration>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ExceptionHandlingTests>();
            extra?.Invoke(cfg);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Exception_handler_can_recover_with_response()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var response = await mediator.Send(new HandledRequest());

        Assert.Equal("recovered", response);
    }

    [Fact]
    public async Task Exception_action_runs_then_exception_propagates()
    {
        var provider = BuildProvider();
        var recorder = provider.GetRequiredService<Recorder>();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.Send(new ActionRequest()));

        Assert.Contains("action", recorder.Messages);
    }

    [Fact]
    public async Task Default_strategy_skips_action_when_handled()
    {
        var provider = BuildProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        var response = await provider.GetRequiredService<IMediator>().Send(new BothRequest());

        Assert.Equal("recovered", response);
        Assert.DoesNotContain("action", recorder.Messages);
    }

    [Fact]
    public async Task ApplyForAllExceptions_runs_action_even_when_handled()
    {
        var provider = BuildProvider(cfg =>
            cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions);
        var recorder = provider.GetRequiredService<Recorder>();

        var response = await provider.GetRequiredService<IMediator>().Send(new BothRequest());

        Assert.Equal("recovered", response);
        Assert.Contains("action", recorder.Messages);
    }
}
