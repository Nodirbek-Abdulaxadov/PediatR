namespace PediatR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PediatR.NotificationPublishers;
using Xunit;

public class RegistrationTests
{
    public class RegPing : IRequest<int>
    {
    }

    public class RegPingHandler : IRequestHandler<RegPing, int>
    {
        public Task<int> Handle(RegPing request, CancellationToken cancellationToken) => Task.FromResult(42);
    }

    [Fact]
    public void AddPediatR_without_assemblies_throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddPediatR(_ => { }));
    }

    [Fact]
    public async Task AddPediatR_with_prebuilt_config_registers_handlers()
    {
        var configuration = new PediatRServiceConfiguration();
        configuration.RegisterServicesFromAssemblyContaining<RegistrationTests>();

        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(configuration);
        var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IMediator>().Send(new RegPing());

        Assert.Equal(42, result);
    }

    [Fact]
    public void Singleton_lifetime_shares_mediator_across_sender_and_publisher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RegistrationTests>();
            cfg.Lifetime = ServiceLifetime.Singleton;
        });
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var sender = provider.GetRequiredService<ISender>();
        var publisher = provider.GetRequiredService<IPublisher>();

        Assert.Same(mediator, sender);
        Assert.Same(mediator, publisher);
    }

    [Fact]
    public void NotificationPublisherType_is_registered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Recorder>();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RegistrationTests>();
            cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
        });
        var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<INotificationPublisher>();

        Assert.IsType<TaskWhenAllPublisher>(publisher);
    }
}
