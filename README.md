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

Multi-targets `netstandard2.0` (broad reach, incl. .NET Framework), `net8.0`, `net9.0` and `net10.0`,
so consumers on the latest runtimes get a natively-built, optimized asset.

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

## Source-generated handlers (optional)

Beyond the classic hand-written `IRequestHandler<,>`, PediatR ships an optional Roslyn source
generator — **bundled in the same package**, no extra install — that removes the request/handler
boilerplate. Annotate an **instance method** and the generator emits the matching request type and
handler for you. The generated request keeps the exact MediatR shape, so it runs through the same
pipeline as everything else and is picked up by the same assembly scan (no extra discovery marker).

Three authoring modes, one pipeline:

```csharp
public sealed class TodoFeatures(AppDbContext db)
{
    // Neutral — no CQRS opinion.  → ListTodosRequest : IRequest<List<Todo>>
    [Handler] public Task<List<Todo>> ListTodos() => db.Todos.ToListAsync();

    // Query marker.               → GetTodoQuery(int Id) : IQuery<Todo?> : IRequest<Todo?>
    [Query]   public Task<Todo?> GetTodo(int id) => db.Todos.FindAsync(id).AsTask();

    // Command marker; the [Authorize] is forwarded onto the generated request.
    //                             → CreateTodoCommand(string Title) : ICommand<int> : IRequest<int>
    [Command] [Authorize(Roles = "admin")]
    public Task<int> CreateTodo(string title) { /* ... */ }
}
```

The declaring class is injected from DI (you register it), and its constructor dependencies flow in
normally. Dispatch either explicitly or through the generated ergonomic extension:

```csharp
var todo  = await sender.Send(new GetTodoQuery(42));  // explicit
var todo2 = await sender.TodoFeatures().GetTodo(42);  // generated grouped ISender proxy
```

Queries and commands are grouped under their host (`sender.TodoFeatures().…`) rather than flattened
onto `ISender` directly, so two features exposing a same-named request (e.g. two `GetAll`s) never
collide at the call site. The accessor is a classic extension method, so it works down to
netstandard2.0.

- **`ICommand<T>` / `IQuery<T>`** are thin markers over `IRequest<T>` that let you target behaviors
  (e.g. `where TRequest : IQuery<TResponse>`). They are additive — hand-written or migrated code that
  only uses `IRequest<T>` is unaffected.
- **Attribute forwarding.** Any attribute on the method that is valid on a class (e.g. `[Authorize]`)
  is copied onto the generated request, so authorization and other reflective behaviors work unchanged.
- **Scope.** The generator handles `Task<T>`-returning instance methods; caching is a planned opt-in
  behavior, not part of this layer.

None of this touches the drop-in guarantee: the generator only reacts to `[Handler]`/`[Command]`/
`[Query]`, so a mechanical `MediatR` → `PediatR` migration (which has none of them) compiles
untouched.

Runnable demos:
- [`samples/PediatR.Sample`](samples/PediatR.Sample) — a console walkthrough of the three modes.
- [`samples/PediatR.Sample.Api`](samples/PediatR.Sample.Api) — a full minimal-API showcase: the
  Clean Architecture pipeline (logging / unhandled / authorization / validation / performance),
  `[Authorize]`, FluentValidation, notification fan-out, and streaming — all driven over HTTP.

## Performance

On .NET 10, PediatR is **faster** than MediatR 12.5.0 on Send, pipelines and streams, and on par for
Publish — with equal or lower allocations. From a BenchmarkDotNet `MediumRun`:

| Scenario | MediatR | PediatR | Ratio | Allocated |
|--------------------------|--------:|--------:|:-----:|:----------------|
| Send                     | 81.3 ns | **75.8 ns** | 0.93× | 200 B → 200 B |
| Publish (2 handlers)     | 165.8 ns | 168.3 ns | 1.02× | 440 B → 440 B |
| Pipeline (3 behaviors)   | 253.7 ns | **193.5 ns** | 0.76× | 728 B → 632 B |
| Stream (10 items)        | 574.9 ns | **288.7 ns** | 0.50× | 584 B → 320 B |

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
