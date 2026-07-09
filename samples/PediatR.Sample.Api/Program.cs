using FluentValidation;
using PediatR;
using PediatR.Sample.Api.Behaviours;
using PediatR.Sample.Api.Domain;
using PediatR.Sample.Api.Features;
using PediatR.Sample.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TodoStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

// Feature hosts — the generated handlers inject these from DI.
builder.Services.AddScoped<TodoQueries>();
builder.Services.AddScoped<TodoCommands>();

// Validators (the template scans the assembly; explicit here for clarity).
builder.Services.AddScoped<IValidator<CreateTodo>, CreateTodoValidator>();

// PediatR + the full Clean Architecture pipeline. `AddMediatR` -> `AddPediatR` is the only change a
// migrating app makes; the generated handlers are discovered by the same assembly scan.
builder.Services.AddPediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(TodoQueries).Assembly);
    cfg.AddOpenRequestPreProcessor(typeof(LoggingPreProcessor<>));
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
});

var app = builder.Build();

// Translate the pipeline's exceptions into HTTP status codes.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { errors = exception.Errors.Select(e => e.ErrorMessage) });
    }
    catch (UnauthorizedAccessException)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
    catch (ForbiddenAccessException)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
});

app.MapGet("/", () => Results.Ok(new { service = "PediatR.Sample.Api", see = "/todos" }));

var todos = app.MapGroup("/todos");

// Mode 1 (classic) + notification fan-out. Requires role user|admin via X-User / X-Roles headers.
todos.MapPost("/", async (CreateTodo command, ISender sender) =>
{
    var todo = await sender.Send(command);
    return Results.Created($"/todos/{todo.Id}", todo);
});

// Modes 2 & 3 (generated) dispatched through the grouped ISender proxy — sender.TodoQueries().X().
todos.MapGet("/", (ISender sender) => sender.TodoQueries().ListTodos());
todos.MapGet("/count", (ISender sender) => sender.TodoQueries().CountTodos());
todos.MapGet("/{id:int}", async (int id, ISender sender)
    => await sender.TodoQueries().GetTodo(id) is { } todo ? Results.Ok(todo) : Results.NotFound());

// [Command] (generated) — explicit Send to show the other dispatch style.
todos.MapPost("/{id:int}/complete", async (int id, ISender sender)
    => await sender.Send(new CompleteTodoCommand(id)) is { } todo ? Results.Ok(todo) : Results.NotFound());

// [Command] + forwarded [Authorize(Roles = "admin")] — enforced by AuthorizationBehaviour.
todos.MapDelete("/{id:int}", async (int id, ISender sender)
    => await sender.Send(new DeleteTodoCommand(id)) ? Results.NoContent() : Results.NotFound());

// Streaming — IStreamRequest projected straight to a streaming JSON response.
todos.MapGet("/stream", (ISender sender, CancellationToken cancellationToken)
    => sender.CreateStream(new StreamTodos(), cancellationToken));

app.Run();
