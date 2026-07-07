# Benchmarks: PediatR vs MediatR

PediatR positions itself as a free, drop-in alternative to MediatR, so its performance has to be
competitive. These [BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks compare PediatR
head-to-head with the real **MediatR 12.5.0** across the four hot paths.

> MediatR (Apache-2.0) is referenced **only** by the benchmark project for comparison — it is never
> a dependency of the shipped PediatR package.

Both sides use structurally identical request/handler/notification/stream types (one set against
`MediatR`'s interfaces, one against `PediatR`'s), resolved from `Microsoft.Extensions.DependencyInjection`.

## Results

Environment: BenchmarkDotNet v0.14.0 · .NET 8.0.28 (RyuJIT AVX-512) · Intel Xeon @ 2.10 GHz, 4 cores ·
Ubuntu 24.04 · `MediumRun` (2 launches × 15 iterations, 10 warmup).

| Scenario | MediatR | PediatR | Ratio (P/M) | Allocated (MediatR → PediatR) |
|--------------------------------|--------:|--------:|:-----------:|:------------------------------|
| Send (request → response)      | 161.6 ns | 166.7 ns | 1.03× | 312 B → 312 B |
| Publish (notification, 2 handlers) | 197.6 ns | 205.5 ns | 1.04× | 440 B → 440 B |
| Send through 3 pipeline behaviors  | 335.4 ns | 329.9 ns | 0.98× | 792 B → 792 B |
| CreateStream (10 items)        | 727.8 ns | 411.9 ns | **0.57×** | 632 B → 432 B |

Ratio is PediatR ÷ MediatR — **lower is better for PediatR**; `1.00×` means parity.

### Takeaways

- **Send / Publish** — within ~3–4% of MediatR, with **byte-for-byte identical allocations**. The
  request and notification paths are as lean as MediatR's.
- **Pipeline behaviors** — effectively identical (PediatR a hair ahead), identical allocations.
- **Streaming** — PediatR is **~1.8× faster** and allocates **~32% less** on `CreateStream`.

Bottom line: switching to the free alternative costs nothing at runtime — PediatR is
performance-competitive with MediatR everywhere and faster for streaming.

## Reproduce

```bash
# Full run (tightest intervals)
dotnet run -c Release --project benchmarks/PediatR.Benchmarks -- --filter '*'

# Quicker sanity check
dotnet run -c Release --project benchmarks/PediatR.Benchmarks -- --filter '*' --job short
```

Absolute nanosecond figures depend on hardware; the **relative** comparison (Ratio / Alloc Ratio) is
the portable result. The benchmark source lives in [`benchmarks/PediatR.Benchmarks`](benchmarks/PediatR.Benchmarks).
