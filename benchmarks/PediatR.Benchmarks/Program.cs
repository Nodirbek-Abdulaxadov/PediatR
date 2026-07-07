using BenchmarkDotNet.Running;
using Benchmarks;

BenchmarkSwitcher.FromTypes(new[] { typeof(MediatorBenchmarks) }).Run(args);
