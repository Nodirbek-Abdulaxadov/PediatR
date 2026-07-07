namespace PediatR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class SendRequestTests
{
    public class Ping : IRequest<Pong>
    {
        public string? Message { get; set; }
    }

    public class Pong
    {
        public string? Message { get; set; }
    }

    public class PingHandler : IRequestHandler<Ping, Pong>
    {
        public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(new Pong { Message = request.Message + " Pong" });
    }

    public class Poke : IRequest
    {
        public string? Message { get; set; }
    }

    public class PokeHandler : IRequestHandler<Poke>
    {
        private readonly Recorder _recorder;

        public PokeHandler(Recorder recorder) => _recorder = recorder;

        public Task Handle(Poke request, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("Poke:" + request.Message);
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SendRequestTests>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Send_request_returns_handler_response()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var response = await mediator.Send(new Ping { Message = "Ping" });

        Assert.Equal("Ping Pong", response.Message);
    }

    [Fact]
    public async Task Send_void_request_invokes_handler()
    {
        var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var recorder = provider.GetRequiredService<Recorder>();

        await mediator.Send(new Poke { Message = "hi" });

        Assert.Contains("Poke:hi", recorder.Messages);
    }

    [Fact]
    public async Task Send_object_returns_boxed_response()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        object? response = await mediator.Send((object)new Ping { Message = "Ping" });

        var pong = Assert.IsType<Pong>(response);
        Assert.Equal("Ping Pong", pong.Message);
    }

    [Fact]
    public async Task Send_via_ISender_resolves_pipeline()
    {
        var sender = BuildProvider().GetRequiredService<ISender>();

        var response = await sender.Send(new Ping { Message = "X" });

        Assert.Equal("X Pong", response.Message);
    }

    [Fact]
    public async Task Send_null_request_throws()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await mediator.Send<Pong>(null!));
    }

    [Fact]
    public async Task Send_object_not_a_request_throws()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentException>(async () => await mediator.Send(new object()));
    }
}
