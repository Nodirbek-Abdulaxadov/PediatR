namespace PediatR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Exercises the incremental source generator end-to-end: methods annotated with
/// <c>[Handler]</c> / <c>[Command]</c> / <c>[Query]</c> on an instance host class should produce
/// request records and handlers that flow through <see cref="IMediator.Send{TResponse}"/> exactly
/// like hand-written ones, and coexist with them.
/// </summary>
public class SourceGeneratorTests
{
    // A stateful dependency, resolved into the host class constructor.
    public sealed class Greeter
    {
        private readonly string _prefix = "Hello";

        public int Total { get; private set; }

        public string Greet(string name) => $"{_prefix}, {name}!";

        public int Record()
        {
            Total++;
            return Total;
        }
    }

    // The authoring host: a plain DI-registered instance class. Its dependencies (Greeter) arrive
    // via the constructor; the generated handlers inject this class and delegate to the methods.
    public sealed class GreetingFeatures
    {
        private readonly Greeter _greeter;

        public GreetingFeatures(Greeter greeter) => _greeter = greeter;

        [Query]
        public Task<string> GetGreeting(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_greeter.Greet(name));

        [Command]
        public Task<int> RecordVisit(string name)
            => Task.FromResult(_greeter.Record());

        [Handler]
        public Task<int> CountVisits()
            => Task.FromResult(_greeter.Total);
    }

    // Hand-written request + handler, living side by side with the generated ones.
    public sealed record ManualPing(string Message) : IRequest<string>;

    public sealed class ManualPingHandler : IRequestHandler<ManualPing, string>
    {
        public Task<string> Handle(ManualPing request, CancellationToken cancellationToken)
            => Task.FromResult($"{request.Message} pong");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Greeter>();
        services.AddTransient<GreetingFeatures>(); // host class registered by the consumer
        services.AddPediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SourceGeneratorTests>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Generated_query_flows_through_send()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new GetGreetingQuery("Ada"));

        Assert.Equal("Hello, Ada!", result);
    }

    [Fact]
    public void Generated_query_carries_IQuery_and_IRequest_markers()
    {
        var request = new GetGreetingQuery("x");

        Assert.IsAssignableFrom<IQuery<string>>(request);
        Assert.IsAssignableFrom<IRequest<string>>(request);
    }

    [Fact]
    public void Generated_command_carries_ICommand_marker()
    {
        Assert.IsAssignableFrom<ICommand<int>>(new RecordVisitCommand("x"));
    }

    [Fact]
    public void Neutral_handler_request_is_plain_IRequest_only()
    {
        // Reflection form (not the `is` operator) keeps the negative assertions from being
        // statically decidable — it is exactly the generated type shape that is under test.
        var requestType = typeof(CountVisitsRequest);

        Assert.True(typeof(IRequest<int>).IsAssignableFrom(requestType));
        Assert.False(typeof(IQuery<int>).IsAssignableFrom(requestType));
        Assert.False(typeof(ICommand<int>).IsAssignableFrom(requestType));
    }

    [Fact]
    public async Task Generated_command_reaches_the_host_service()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var count = await mediator.Send(new RecordVisitCommand("Bob"));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Generated_and_manual_handlers_coexist()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var generated = await mediator.Send(new GetGreetingQuery("Bob"));
        var manual = await mediator.Send(new ManualPing("ping"));

        Assert.Equal("Hello, Bob!", generated);
        Assert.Equal("ping pong", manual);
    }
}
