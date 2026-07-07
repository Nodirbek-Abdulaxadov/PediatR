namespace PediatR.Tests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PediatR.NotificationPublishers;
using Xunit;

public class NotificationTests
{
    public class Pinged : INotification
    {
    }

    public class PingedHandlerOne : INotificationHandler<Pinged>
    {
        private readonly Recorder _recorder;

        public PingedHandlerOne(Recorder recorder) => _recorder = recorder;

        public Task Handle(Pinged notification, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("one");
            return Task.CompletedTask;
        }
    }

    public class PingedHandlerTwo : INotificationHandler<Pinged>
    {
        private readonly Recorder _recorder;

        public PingedHandlerTwo(Recorder recorder) => _recorder = recorder;

        public Task Handle(Pinged notification, CancellationToken cancellationToken)
        {
            _recorder.Messages.Add("two");
            return Task.CompletedTask;
        }
    }

    public class SyncPinged : INotification
    {
    }

    public class SyncPingedHandler : NotificationHandler<SyncPinged>
    {
        private readonly Recorder _recorder;

        public SyncPingedHandler(Recorder recorder) => _recorder = recorder;

        protected override void Handle(SyncPinged notification) => _recorder.Messages.Add("sync");
    }

    private static ServiceProvider BuildProvider(System.Action<PediatRServiceConfiguration>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NotificationTests>();
            extra?.Invoke(cfg);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Publish_invokes_all_handlers_foreach()
    {
        var provider = BuildProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IMediator>().Publish(new Pinged());

        Assert.Contains("one", recorder.Messages);
        Assert.Contains("two", recorder.Messages);
    }

    [Fact]
    public async Task Publish_invokes_all_handlers_when_all()
    {
        var provider = BuildProvider(cfg => cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher));
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IPublisher>().Publish(new Pinged());

        Assert.Contains("one", recorder.Messages);
        Assert.Contains("two", recorder.Messages);
    }

    [Fact]
    public async Task Publish_object_invokes_handlers()
    {
        var provider = BuildProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IMediator>().Publish((object)new Pinged());

        Assert.Equal(2, recorder.Messages.Count);
    }

    [Fact]
    public async Task NotificationHandler_base_runs()
    {
        var provider = BuildProvider();
        var recorder = provider.GetRequiredService<Recorder>();

        await provider.GetRequiredService<IMediator>().Publish(new SyncPinged());

        Assert.Contains("sync", recorder.Messages);
    }
}
