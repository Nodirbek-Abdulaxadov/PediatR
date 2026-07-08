namespace PediatR.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PediatR.Pipeline;
using Xunit;

/// <summary>
/// Phase 4 — Clean Architecture acceptance. This reproduces the pipeline of Jason Taylor's
/// CleanArchitecture template: the same four behaviors (Unhandled / Authorization / Validation /
/// Performance) plus the logging pre-processor, the same <c>[Authorize]</c>-on-the-request model,
/// real FluentValidation validators, and the same registration block — with the single change the
/// migration prescribes, <c>AddMediatR</c> → <c>AddPediatR</c>.
///
/// All three authoring modes flow through it: classic hand-written handlers, <c>[Handler]</c>, and
/// <c>[Command]</c>/<c>[Query]</c>. If this compiles and passes, the template works after a
/// mechanical find-and-replace.
/// </summary>
public class CleanArchitectureAcceptanceTests
{
    // ── Security primitives (mirror Application.Common.Security) ──────────────
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AuthorizeAttribute : Attribute
    {
        public string Roles { get; set; } = string.Empty;

        public string Policy { get; set; } = string.Empty;
    }

    public sealed class ForbiddenAccessException : Exception
    {
    }

    public interface ICurrentUser
    {
        string? Id { get; }

        bool IsInRole(string role);

        bool Authorize(string policy);
    }

    public sealed class FakeUser : ICurrentUser
    {
        public string? Id { get; set; } = "user-1";

        public HashSet<string> Roles { get; } = new();

        public HashSet<string> Policies { get; } = new();

        public bool IsInRole(string role) => Roles.Contains(role);

        public bool Authorize(string policy) => Policies.Contains(policy);
    }

    public sealed class PipelineLog
    {
        public List<string> Steps { get; } = new();
    }

    // ── The four behaviors + pre-processor, shaped like the template ─────────
    public sealed class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
        where TRequest : notnull
    {
        private readonly PipelineLog _log;

        public LoggingBehaviour(PipelineLog log) => _log = log;

        public Task Process(TRequest request, CancellationToken cancellationToken)
        {
            _log.Steps.Add("logging");
            return Task.CompletedTask;
        }
    }

    public sealed class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly PipelineLog _log;

        public UnhandledExceptionBehaviour(PipelineLog log) => _log = log;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _log.Steps.Add("unhandled");
            try
            {
                return await next(cancellationToken);
            }
            catch
            {
                _log.Steps.Add("unhandled:caught");
                throw;
            }
        }
    }

    public sealed class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ICurrentUser _user;
        private readonly PipelineLog _log;

        public AuthorizationBehaviour(ICurrentUser user, PipelineLog log)
        {
            _user = user;
            _log = log;
        }

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _log.Steps.Add("authorization");

            var attributes = typeof(TRequest).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
            if (attributes.Length > 0)
            {
                if (_user.Id is null)
                {
                    throw new UnauthorizedAccessException();
                }

                foreach (var attribute in attributes.Where(a => !string.IsNullOrWhiteSpace(a.Roles)))
                {
                    var authorized = attribute.Roles
                        .Split(',')
                        .Select(role => role.Trim())
                        .Any(_user.IsInRole);

                    if (!authorized)
                    {
                        throw new ForbiddenAccessException();
                    }
                }

                foreach (var attribute in attributes.Where(a => !string.IsNullOrWhiteSpace(a.Policy)))
                {
                    if (!_user.Authorize(attribute.Policy))
                    {
                        throw new ForbiddenAccessException();
                    }
                }
            }

            return next(cancellationToken);
        }
    }

    public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        private readonly PipelineLog _log;

        public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators, PipelineLog log)
        {
            _validators = validators;
            _log = log;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _log.Steps.Add("validation");

            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);
                var failures = _validators
                    .Select(validator => validator.Validate(context))
                    .SelectMany(result => result.Errors)
                    .Where(failure => failure is not null)
                    .ToList();

                if (failures.Count != 0)
                {
                    throw new ValidationException(failures);
                }
            }

            return await next(cancellationToken);
        }
    }

    public sealed class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly PipelineLog _log;

        public PerformanceBehaviour(PipelineLog log) => _log = log;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _log.Steps.Add("performance");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var response = await next(cancellationToken);
            timer.Stop();
            return response;
        }
    }

    // ── A vertical slice using all three authoring modes ─────────────────────
    public sealed record TodoDto(int Id, string Title);

    public sealed class TodoStore
    {
        private readonly Dictionary<int, string> _items = new();
        private int _next = 1;

        public int Add(string title)
        {
            var id = _next++;
            _items[id] = title;
            return id;
        }

        public bool Remove(int id) => _items.Remove(id);

        public TodoDto? Get(int id) => _items.TryGetValue(id, out var title) ? new TodoDto(id, title) : null;

        public int Count => _items.Count;
    }

    // Mode 1 — classic hand-written request + handler; [Authorize] on the request class.
    [Authorize(Policy = "todo:write")]
    public sealed record CreateTodo(string Title) : IRequest<int>;

    public sealed class CreateTodoHandler : IRequestHandler<CreateTodo, int>
    {
        private readonly TodoStore _store;

        public CreateTodoHandler(TodoStore store) => _store = store;

        public Task<int> Handle(CreateTodo request, CancellationToken cancellationToken)
            => Task.FromResult(_store.Add(request.Title));
    }

    public sealed class CreateTodoValidator : AbstractValidator<CreateTodo>
    {
        public CreateTodoValidator() => RuleFor(x => x.Title).NotEmpty();
    }

    // Modes 2 & 3 — generated from an instance host.
    public sealed class TodoFeatures
    {
        private readonly TodoStore _store;

        public TodoFeatures(TodoStore store) => _store = store;

        [Query]
        public Task<TodoDto?> GetTodo(int id) => Task.FromResult(_store.Get(id));

        [Command]
        [Authorize(Roles = "admin")]
        public Task<bool> DeleteTodo(int id) => Task.FromResult(_store.Remove(id));

        [Handler]
        public Task<int> CountTodos() => Task.FromResult(_store.Count);

        [Handler]
        public Task<int> Boom()
        {
            _ = _store.Count; // touch instance state, then fail — exercises UnhandledExceptionBehaviour
            throw new InvalidOperationException("boom");
        }
    }

    // A real FluentValidation validator bound to a GENERATED request type.
    public sealed class GetTodoValidator : AbstractValidator<GetTodoQuery>
    {
        public GetTodoValidator() => RuleFor(x => x.Id).GreaterThan(0);
    }

    [Fact]
    public async Task Full_pipeline_runs_for_a_generated_query()
    {
        await using var provider = Build();
        var log = provider.GetRequiredService<PipelineLog>();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new GetTodoQuery(1));

        Assert.Equal(
            new[] { "logging", "unhandled", "authorization", "validation", "performance" },
            log.Steps);
    }

    [Fact]
    public async Task All_three_authoring_modes_dispatch()
    {
        await using var provider = Build(user =>
        {
            user.Policies.Add("todo:write");
            user.Roles.Add("admin");
        });
        var sender = provider.GetRequiredService<ISender>();

        var createdId = await sender.Send(new CreateTodo("Buy milk")); // classic
        var count = await sender.Send(new CountTodosRequest());          // [Handler]
        var todo = await sender.Send(new GetTodoQuery(createdId));       // [Query]
        var removed = await sender.Send(new DeleteTodoCommand(createdId)); // [Command]

        Assert.Equal(1, createdId);
        Assert.Equal(1, count);
        Assert.Equal("Buy milk", todo!.Title);
        Assert.True(removed);
    }

    [Fact]
    public async Task Classic_request_authorization_enforced()
    {
        await using var denied = Build(); // authenticated but lacks the policy
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => denied.GetRequiredService<ISender>().Send(new CreateTodo("x")));

        await using var allowed = Build(user => user.Policies.Add("todo:write"));
        var id = await allowed.GetRequiredService<ISender>().Send(new CreateTodo("x"));
        Assert.Equal(1, id);
    }

    [Fact]
    public async Task Forwarded_authorize_on_generated_command_enforced()
    {
        await using var denied = Build(); // not in role "admin"
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => denied.GetRequiredService<ISender>().Send(new DeleteTodoCommand(1)));

        await using var allowed = Build(user => user.Roles.Add("admin"));
        var removed = await allowed.GetRequiredService<ISender>().Send(new DeleteTodoCommand(1));
        Assert.False(removed); // nothing to remove, but the call was authorized and ran
    }

    [Fact]
    public async Task Fluent_validation_on_generated_request_short_circuits()
    {
        await using var provider = Build();
        var log = provider.GetRequiredService<PipelineLog>();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new GetTodoQuery(0)));

        Assert.Contains("validation", log.Steps);
        Assert.DoesNotContain("performance", log.Steps); // inner behavior never reached
    }

    [Fact]
    public async Task Unhandled_exception_behaviour_observes_and_rethrows()
    {
        await using var provider = Build();
        var log = provider.GetRequiredService<PipelineLog>();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new BoomRequest()));

        Assert.Contains("unhandled:caught", log.Steps);
    }

    private static ServiceProvider Build(Action<FakeUser>? configureUser = null)
    {
        var user = new FakeUser();
        configureUser?.Invoke(user);

        var services = new ServiceCollection();
        services.AddSingleton<PipelineLog>();
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<TodoStore>();
        services.AddTransient<TodoFeatures>();

        services.AddScoped<IValidator<CreateTodo>, CreateTodoValidator>();
        services.AddScoped<IValidator<GetTodoQuery>, GetTodoValidator>();

        // The registration block straight out of the template, AddMediatR -> AddPediatR.
        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CleanArchitectureAcceptanceTests>();
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
        });

        return services.BuildServiceProvider();
    }
}
