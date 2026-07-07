namespace Benchmarks;

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Benchmarks.MediatrLib;
using Benchmarks.PediatrLib;

/// <summary>
/// Head-to-head microbenchmarks of PediatR against the real MediatR 12.5.0, across the four
/// hot paths: Send (request/response), Publish (a notification with two handlers), a Send that
/// flows through three no-op pipeline behaviors, and consuming a 10-item stream.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MediatorBenchmarks
{
    private MediatR.IMediator _mediatr = null!;
    private PediatR.IMediator _pediatr = null!;
    private MediatR.IMediator _mediatrPipeline = null!;
    private PediatR.IMediator _pediatrPipeline = null!;

    private readonly MPing _mPing = new();
    private readonly PPing _pPing = new();
    private readonly MNotify _mNotify = new();
    private readonly PNotify _pNotify = new();
    private readonly MStream _mStream = new();
    private readonly PStream _pStream = new();

    [GlobalSetup]
    public void Setup()
    {
        _mediatr = BuildMediatr(withPipeline: false);
        _mediatrPipeline = BuildMediatr(withPipeline: true);
        _pediatr = BuildPediatr(withPipeline: false);
        _pediatrPipeline = BuildPediatr(withPipeline: true);
    }

    private static MediatR.IMediator BuildMediatr(bool withPipeline)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MPing).Assembly);
            if (withPipeline)
            {
                cfg.AddOpenBehavior(typeof(MBehaviorOne<,>));
                cfg.AddOpenBehavior(typeof(MBehaviorTwo<,>));
                cfg.AddOpenBehavior(typeof(MBehaviorThree<,>));
            }
        });
        return services.BuildServiceProvider().GetRequiredService<MediatR.IMediator>();
    }

    private static PediatR.IMediator BuildPediatr(bool withPipeline)
    {
        var services = new ServiceCollection();
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(PPing).Assembly);
            if (withPipeline)
            {
                cfg.AddOpenBehavior(typeof(PBehaviorOne<,>));
                cfg.AddOpenBehavior(typeof(PBehaviorTwo<,>));
                cfg.AddOpenBehavior(typeof(PBehaviorThree<,>));
            }
        });
        return services.BuildServiceProvider().GetRequiredService<PediatR.IMediator>();
    }

    [BenchmarkCategory("Send"), Benchmark(Baseline = true)]
    public Task<string> MediatR_Send() => _mediatr.Send(_mPing);

    [BenchmarkCategory("Send"), Benchmark]
    public Task<string> PediatR_Send() => _pediatr.Send(_pPing);

    [BenchmarkCategory("Publish"), Benchmark(Baseline = true)]
    public Task MediatR_Publish() => _mediatr.Publish(_mNotify);

    [BenchmarkCategory("Publish"), Benchmark]
    public Task PediatR_Publish() => _pediatr.Publish(_pNotify);

    [BenchmarkCategory("Pipeline (3 behaviors)"), Benchmark(Baseline = true)]
    public Task<string> MediatR_Pipeline() => _mediatrPipeline.Send(_mPing);

    [BenchmarkCategory("Pipeline (3 behaviors)"), Benchmark]
    public Task<string> PediatR_Pipeline() => _pediatrPipeline.Send(_pPing);

    [BenchmarkCategory("Stream (10 items)"), Benchmark(Baseline = true)]
    public async Task<int> MediatR_Stream()
    {
        var sum = 0;
        await foreach (var item in _mediatr.CreateStream(_mStream))
        {
            sum += item;
        }

        return sum;
    }

    [BenchmarkCategory("Stream (10 items)"), Benchmark]
    public async Task<int> PediatR_Stream()
    {
        var sum = 0;
        await foreach (var item in _pediatr.CreateStream(_pStream))
        {
            sum += item;
        }

        return sum;
    }
}
