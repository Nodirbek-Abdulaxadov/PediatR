# Benchmarks: PediatR vs MediatR

PediatR positions itself as a free, drop-in alternative to MediatR, so its performance has to be
competitive. These [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks compare PediatR
head-to-head with the real **MediatR 12.5.0** across the four hot paths.

> MediatR (Apache-2.0) is referenced **only** by the benchmark project for comparison — it is never
> a dependency of the shipped PediatR package.

Both sides use structurally identical request/handler/notification/stream types (one set against
`MediatR`'s interfaces, one against `PediatR`'s), resolved from `Microsoft.Extensions.DependencyInjection`.

## Results

Environment: BenchmarkDotNet v0.14.0 · **.NET 10.0.9** (RyuJIT AVX-512) · Intel Xeon @ 2.10 GHz, 4 cores ·
Ubuntu 24.04 · `MediumRun` (2 launches × 15 iterations, 10 warmup).

| Scenario | MediatR | PediatR | Ratio (P/M) | Allocated (MediatR → PediatR) |
|--------------------------------|--------:|--------:|:-----------:|:------------------------------|
| Send (request → response)      | 81.3 ns | **75.8 ns** | **0.93×** | 200 B → 200 B |
| Publish (notification, 2 handlers) | 165.8 ns | 168.3 ns | 1.02× | 440 B → 440 B |
| Send through 3 pipeline behaviors  | 253.7 ns | **193.5 ns** | **0.76×** | 728 B → 632 B |
| CreateStream (10 items)        | 574.9 ns | **288.7 ns** | **0.50×** | 584 B → 320 B |

Ratio is PediatR ÷ MediatR — **lower is better for PediatR**; `1.00×` means parity.

### Takeaways

- **Send** — PediatR is **~7% faster** with identical allocations.
- **Publish** — parity (~2%), byte-for-byte identical allocations. (Notifications have no behavior
  pipeline, so both do the same work.)
- **Pipeline behaviors** — PediatR is **~24% faster** and allocates **~13% less**: the pipeline is
  folded with an array walk that skips the LINQ `Reverse().Aggregate()` buffer and accumulator
  closures MediatR builds per request.
- **Streaming** — PediatR is **~2× faster** and allocates **~45% less** on `CreateStream`.

Bottom line: switching to the free alternative doesn't just cost nothing at runtime — on .NET 10,
PediatR is **faster** than MediatR on Send, pipelines and streams, and on par for Publish.

> **Note on the comparison.** MediatR 12.5.0 ships assets only up to `net6.0`, so on a .NET 10 host it
> runs its `net6.0` build; PediatR ships a native `net10.0` build. That is exactly the code a .NET 10
> app consumes from each package, so it is a fair as-installed comparison — and shipping a
> current-runtime asset is part of PediatR's edge.

## Reproduce

```bash
# Full run (tightest intervals)
dotnet run -c Release --project benchmarks/PediatR.Benchmarks -- --filter '*'

# Quicker sanity check
dotnet run -c Release --project benchmarks/PediatR.Benchmarks -- --filter '*' --job short
```

Absolute nanosecond figures depend on hardware; the **relative** comparison (Ratio / Alloc Ratio) is
the portable result. The benchmark source lives in [`benchmarks/PediatR.Benchmarks`](benchmarks/PediatR.Benchmarks).
