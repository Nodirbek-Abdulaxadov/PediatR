# PediatR

**PediatR** is a small, dependency-light mediator library for .NET whose public API is a
source-compatible reimplementation of the [MediatR](https://github.com/jbogard/MediatR) **12.5.0**
contract, under the `PediatR` namespace instead of `MediatR`.

It is written from scratch (clean-room): only the *shape* of the public API — interface names,
method signatures, generic variance and observable behavior of the mediator pattern — is mirrored.
No MediatR implementation code is used. PediatR ships under the permissive **MIT** license.

> The mediator pattern belongs to nobody. PediatR reproduces a familiar **API contract** so you can
> adopt it with a mechanical find-and-replace, and keep the rest of your code unchanged.

## Why

If you have code written against MediatR and want a drop-in replacement, replace the string
`MediatR` with `PediatR` in two places:

1. the **package** reference (`MediatR` → `PediatR`), and
2. the **namespace** in your `using` directives (`using MediatR;` → `using PediatR;`).

Everything else — your `IRequest<T>`, `IRequestHandler<,>`, `INotification`, `IPipelineBehavior<,>`,
`IStreamRequestHandler<,>`, pre/post processors, exception handlers, and the
`services.AddMediatR(...)` → `services.AddPediatR(...)` registration call — keeps compiling and
behaving the same way.

The DI entry points live in the `Microsoft.Extensions.DependencyInjection` namespace (unchanged), so
`AddMediatR` becomes `AddPediatR` and `MediatRServiceConfiguration` becomes
`PediatRServiceConfiguration` through the same find-and-replace.

## Install

```
dotnet add package PediatR
```

Targets `netstandard2.0` (broad reach, incl. .NET Framework) and `net8.0`.

## Quick start

```csharp
using PediatR;
using Microsoft.Extensions.DependencyInjection;

// 1. Define a request and its handler
public record Ping(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult($"{request.Message} Pong");
}

// 2. Register PediatR, pointing it at the assemblies to scan
var services = new ServiceCollection();
services.AddPediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());
var provider = services.BuildServiceProvider();

// 3. Send
var mediator = provider.GetRequiredService<IMediator>();
string response = await mediator.Send(new Ping("Ping")); // "Ping Pong"
```

## What's included

- **Sending**: `ISender` / `IMediator.Send<TResponse>(IRequest<TResponse>)`, the void
  `Send<TRequest>(TRequest)` for `IRequest`, and the runtime-typed `Send(object)`.
- **Requests & handlers**: `IRequest`, `IRequest<TResponse>`, `IBaseRequest`,
  `IRequestHandler<,>`, `IRequestHandler<>`, and the `Unit` type.
- **Notifications**: `INotification`, `INotificationHandler<>`, the `NotificationHandler<>` base
  class, and publish strategies `ForeachAwaitPublisher` (sequential) and `TaskWhenAllPublisher`
  (concurrent), selectable via `INotificationPublisher` / `NotificationHandlerExecutor`.
- **Streaming**: `IStreamRequest<TResponse>`, `IStreamRequestHandler<,>`,
  `IStreamPipelineBehavior<,>` and `IMediator.CreateStream(...)` over `IAsyncEnumerable<T>`.
- **Pipeline behaviors**: `IPipelineBehavior<,>` with `RequestHandlerDelegate<TResponse>`.
- **Pre/post processing**: `IRequestPreProcessor<>`, `IRequestPostProcessor<,>` and their behaviors.
- **Exception handling**: `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>`,
  `RequestExceptionHandlerState<TResponse>`, and the `RequestExceptionActionProcessorStrategy`.
- **Registration**: `AddPediatR(...)`, the full `PediatRServiceConfiguration` surface
  (`RegisterServicesFromAssembly*`, `AddBehavior`, `AddOpenBehavior(s)`, `AddStreamBehavior`,
  `AddRequestPreProcessor`, `AddRequestPostProcessor`, `Lifetime`, `NotificationPublisher(Type)`, …).

## Performance

PediatR is performance-competitive with MediatR 12.5.0 — within a few nanoseconds and with identical
allocations on the Send/Publish/pipeline paths, and faster for streaming. From a BenchmarkDotNet
`MediumRun` on .NET 8:

| Scenario | MediatR | PediatR | Ratio | Allocated |
|--------------------------|--------:|--------:|:-----:|:----------------|
| Send                     | 161.6 ns | 166.7 ns | 1.03× | 312 B → 312 B |
| Publish (2 handlers)     | 197.6 ns | 205.5 ns | 1.04× | 440 B → 440 B |
| Pipeline (3 behaviors)   | 335.4 ns | 329.9 ns | 0.98× | 792 B → 792 B |
| Stream (10 items)        | 727.8 ns | 411.9 ns | 0.57× | 632 B → 432 B |

Ratio is PediatR ÷ MediatR (lower is better). See [BENCHMARKS.md](BENCHMARKS.md) for methodology and
how to reproduce.

## Pipeline execution order

For a request, behaviors execute outermost → innermost in this order (the first-registered behavior
is the outermost):

1. Exception action behavior, then exception handler behavior *(for the default
   `ApplyForUnhandledExceptions` strategy; swapped for `ApplyForAllExceptions`)*
2. Request pre-processors
3. Request post-processors
4. Your registered `IPipelineBehavior<,>`s, in registration order
5. The request handler (innermost)

Pre-processors therefore run before your behaviors, post-processors run after them, and exception
handling wraps everything.

## Differences from MediatR

PediatR aims for source-and-behavior compatibility for the mainstream API. A few deliberate notes:

- **Single package.** All contract types (`IRequest`, `INotification`, `Unit`, `IStreamRequest`,
  …) live in the one `PediatR` assembly under the same namespaces; there is no separate
  `PediatR.Contracts` package.
- **Handler lifetime.** Discovered handlers are registered using `PediatRServiceConfiguration.Lifetime`
  (default `Transient`). The built-in pre/post/exception behaviors are always `Transient`.
- **`AutoRegisterRequestProcessors`.** When enabled, auto-discovered pre/post processors are both
  registered *and* wired into the pipeline so they run.
- **Generic handlers.** Closed handlers and identity open-generic handlers
  (e.g. `Handler<T> : INotificationHandler<T>`) are fully supported. The exotic
  nested-generic closing controlled by `RegisterGenericHandlers` (default `false`) and the
  `MaxGenericType*` limits are present on the configuration for API compatibility but not
  exhaustively generated.

## Building & testing

```
dotnet build PediatR.sln -c Release
dotnet test  PediatR.sln -c Release
```

CI (GitHub Actions) builds both target frameworks and runs the xUnit suite on every push and PR.

## License & attribution

PediatR is licensed under the [MIT License](LICENSE). It is an independent implementation of the
mediator pattern and is **not affiliated with, endorsed by, or derived from the source code of
MediatR**. MediatR is a trademark/project of its respective authors and is licensed separately.
Only PediatR's public API contract intentionally matches MediatR's so that migration is mechanical.
