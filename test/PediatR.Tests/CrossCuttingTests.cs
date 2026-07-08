namespace PediatR.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Phase 3 — cross-cutting integration. Proves that (1) non-authoring attributes on the annotated
/// method are forwarded onto the generated request so behaviors that reflect over the request type
/// (authorization) light up, (2) generated requests flow through ordinary pipeline behaviors
/// (validation), and (3) the generated ergonomic <see cref="ISender"/> extension dispatches.
/// </summary>
public class CrossCuttingTests
{
    // A cross-cutting attribute, valid on both methods (authoring site) and classes (generated site).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireRoleAttribute : Attribute
    {
        public RequireRoleAttribute(string role) => Role = role;

        public string Role { get; }
    }

    public sealed class CurrentUser
    {
        public HashSet<string> Roles { get; } = new();
    }

    // Mirrors the Clean Architecture AuthorizationBehaviour: reflect the requirement off the request.
    public sealed class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly CurrentUser _user;

        public AuthorizationBehaviour(CurrentUser user) => _user = user;

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            foreach (var requirement in typeof(TRequest).GetCustomAttributes<RequireRoleAttribute>(inherit: true))
            {
                if (!_user.Roles.Contains(requirement.Role))
                {
                    throw new UnauthorizedAccessException($"Missing role '{requirement.Role}'.");
                }
            }

            return next(cancellationToken);
        }
    }

    // Minimal validation abstraction — no FluentValidation dependency needed to prove the point.
    public interface IValidator<in T>
    {
        IEnumerable<string> Validate(T instance);
    }

    public sealed class ValidationException : Exception
    {
        public ValidationException(IEnumerable<string> errors) : base("Validation failed.")
            => Errors = errors.ToArray();

        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var errors = _validators.SelectMany(v => v.Validate(request)).ToList();
            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }

            return next(cancellationToken);
        }
    }

    // A dependency the host resolves through its constructor (the instance-host model).
    public sealed class Ledger
    {
        public List<string> Entries { get; } = new();
    }

    // Host with cross-cutting concerns declared at the method (authoring) site.
    public sealed class SecuredFeatures
    {
        private readonly Ledger _ledger;

        public SecuredFeatures(Ledger ledger) => _ledger = ledger;

        [Command]
        [RequireRole("admin")]
        public Task<int> DeleteThing(int id)
        {
            _ledger.Entries.Add($"delete:{id}");
            return Task.FromResult(id);
        }

        [Command]
        public Task<string> CreateThing(string name)
        {
            _ledger.Entries.Add($"create:{name}");
            return Task.FromResult(name.ToUpperInvariant());
        }

        [Query]
        public Task<string> Echo(string text)
        {
            _ledger.Entries.Add($"echo:{text}");
            return Task.FromResult(text);
        }
    }

    public sealed class CreateThingValidator : IValidator<CreateThingCommand>
    {
        public IEnumerable<string> Validate(CreateThingCommand instance)
        {
            if (string.IsNullOrWhiteSpace(instance.Name))
            {
                yield return "Name is required.";
            }
        }
    }

    [Fact]
    public void RequireRole_attribute_is_forwarded_onto_generated_request()
    {
        var attribute = typeof(DeleteThingCommand).GetCustomAttribute<RequireRoleAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("admin", attribute!.Role);
    }

    [Fact]
    public async Task Authorization_behaviour_allows_when_role_present()
    {
        await using var provider = BuildProvider(user => user.Roles.Add("admin"), authorization: true);
        var sender = provider.GetRequiredService<ISender>();

        Assert.Equal(7, await sender.Send(new DeleteThingCommand(7)));
    }

    [Fact]
    public async Task Authorization_behaviour_blocks_when_role_missing()
    {
        await using var provider = BuildProvider(_ => { }, authorization: true);
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sender.Send(new DeleteThingCommand(7)));
    }

    [Fact]
    public async Task Validation_behaviour_runs_on_generated_request()
    {
        await using var provider = BuildProvider(_ => { }, validation: true);
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateThingCommand("")));
        Assert.Equal("OK", await sender.Send(new CreateThingCommand("ok")));
    }

    [Fact]
    public async Task Generated_ISender_extension_dispatches()
    {
        await using var provider = BuildProvider(_ => { });
        var sender = provider.GetRequiredService<ISender>();

        // Ergonomic call site — no `new EchoQuery(...)` in sight.
        Assert.Equal("hi", await sender.Echo("hi"));
    }

    private static ServiceProvider BuildProvider(Action<CurrentUser> configureUser, bool authorization = false, bool validation = false)
    {
        var user = new CurrentUser();
        configureUser(user);

        var services = new ServiceCollection();
        services.AddSingleton(user);
        services.AddSingleton<Ledger>();
        services.AddTransient<SecuredFeatures>();
        if (validation)
        {
            services.AddTransient<IValidator<CreateThingCommand>, CreateThingValidator>();
        }

        services.AddPediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CrossCuttingTests>();
            if (authorization)
            {
                cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            }

            if (validation)
            {
                cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            }
        });

        return services.BuildServiceProvider();
    }
}
