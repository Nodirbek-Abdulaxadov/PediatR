# PediatR.Sample.Api

A minimal-API showcase of **everything PediatR can do**, wired the Clean Architecture way — the same
shape a Jason Taylor template uses, with `AddMediatR` → `AddPediatR` as the only change.

## What it demonstrates

| Capability | Where |
|---|---|
| **Mode 1 — classic** hand-written `IRequest`/`IRequestHandler` | `Features/CreateTodo.cs` |
| **Mode 2 — `[Handler]`** (neutral, generated) | `TodoQueries.CountTodos` |
| **Mode 3 — `[Command]`/`[Query]`** (generated) | `TodoQueries`, `TodoCommands` |
| **Full pipeline**: logging pre-processor, unhandled-exception, authorization, validation, performance | `Behaviours/PipelineBehaviours.cs` |
| **Authorization via `[Authorize]`** — on a classic class *and* forwarded from a `[Command]` method | `CreateTodo`, `TodoCommands.DeleteTodo` |
| **FluentValidation** binding to a request | `CreateTodoValidator` |
| **Notifications** (one event, two handlers, fan-out) | `Features/TodoNotifications.cs` |
| **Streaming** (`IStreamRequest` → `IAsyncEnumerable`) | `Features/StreamTodos.cs` |
| **Ergonomic `ISender` extensions** (generated) | `sender.ListTodos()`, `sender.GetTodo(id)` |

## Run

```bash
dotnet run --project samples/PediatR.Sample.Api --urls http://localhost:5080
```

Identity is taken from headers so you can drive authorization from the shell: `X-User` sets the
current user, `X-Roles` is a comma-separated role list.

## Endpoints

```bash
# List / count / get (generated queries, via ISender extensions)
curl http://localhost:5080/todos
curl http://localhost:5080/todos/count
curl http://localhost:5080/todos/1

# Create (classic command): needs role user or admin — else 401/403.
curl -X POST http://localhost:5080/todos \
  -H 'Content-Type: application/json' -H 'X-User: ada' -H 'X-Roles: user' \
  -d '{"title":"Write docs"}'
# → 201, and the server logs AUDIT + EMAIL notification handlers firing.

# Validation failure → 400 with error list.
curl -X POST http://localhost:5080/todos \
  -H 'Content-Type: application/json' -H 'X-User: ada' -H 'X-Roles: user' \
  -d '{"title":""}'

# Complete (generated command).
curl -X POST http://localhost:5080/todos/1/complete

# Delete (generated command + forwarded [Authorize(Roles=admin)]).
curl -X DELETE http://localhost:5080/todos/1 -H 'X-User: ada' -H 'X-Roles: user'   # → 403
curl -X DELETE http://localhost:5080/todos/1 -H 'X-User: ada' -H 'X-Roles: admin'  # → 204

# Streaming (chunked JSON).
curl -N http://localhost:5080/todos/stream
```

Watch the console: every request is bracketed by the `→ Request` / `← Request in Nms` behavior logs,
proving the whole pipeline runs for generated and hand-written requests alike.
