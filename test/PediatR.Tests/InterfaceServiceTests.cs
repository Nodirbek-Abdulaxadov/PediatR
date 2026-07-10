namespace PediatR.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Interface-host authoring (Commandor's service style), kept MediatR-shaped: attributes on a
/// service interface, a DI-registered implementation, pre-shaped request DTOs used directly
/// (pass-through) alongside plain-type parameters (wrapper). Everything still dispatches through
/// <see cref="ISender.Send{TResponse}"/> and the generated grouped proxy.
/// </summary>
public class InterfaceServiceTests
{
    public sealed record TodoView(Guid Id, string Title);

    // Pre-shaped request DTOs — the natural MediatR shape.
    public sealed record CreateTodoCommand(string Title) : ICommand<TodoView>;

    public sealed record GetTodoByIdQuery(Guid Id) : IQuery<TodoView>;

    // Service interface: attributes on interface methods.
    public interface ITodoService
    {
        [Command] Task<TodoView> CreateAsync(CreateTodoCommand command, CancellationToken cancellationToken = default);

        [Query] Task<TodoView> GetByRequestAsync(GetTodoByIdQuery query, CancellationToken cancellationToken = default); // pass-through

        [Query] Task<TodoView> GetAsync(Guid id, CancellationToken cancellationToken = default);                         // plain-type wrapper
    }

    public sealed class TodoService : ITodoService
    {
        public Task<TodoView> CreateAsync(CreateTodoCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(new TodoView(Guid.NewGuid(), command.Title));

        public Task<TodoView> GetByRequestAsync(GetTodoByIdQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new TodoView(query.Id, "by-request"));

        public Task<TodoView> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(new TodoView(id, "by-id"));
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITodoService, TodoService>(); // register the interface implementation
        services.AddPediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<InterfaceServiceTests>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pass_through_command_uses_the_DTO_directly_as_the_request()
    {
        await using var provider = Build();
        var sender = provider.GetRequiredService<ISender>();

        // No generated wrapper — CreateTodoCommand itself is the request.
        var view = await sender.Send(new CreateTodoCommand("Buy milk"));

        Assert.Equal("Buy milk", view.Title);
    }

    [Fact]
    public async Task Pass_through_query_dispatches_through_the_grouped_proxy()
    {
        await using var provider = Build();
        var sender = provider.GetRequiredService<ISender>();

        var id = Guid.NewGuid();
        // Accessor strips the leading I: sender.TodoService(), not sender.ITodoService().
        var view = await sender.TodoService().GetByRequestAsync(new GetTodoByIdQuery(id));

        Assert.Equal(id, view.Id);
        Assert.Equal("by-request", view.Title);
    }

    [Fact]
    public async Task Plain_type_parameter_on_an_interface_generates_a_wrapper()
    {
        await using var provider = Build();
        var sender = provider.GetRequiredService<ISender>();

        var id = Guid.NewGuid();
        var view = await sender.TodoService().GetAsync(id); // GetAsyncQuery(Guid Id) generated

        Assert.Equal(id, view.Id);
        Assert.Equal("by-id", view.Title);
    }
}
