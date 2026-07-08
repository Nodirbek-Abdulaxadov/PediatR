namespace PediatR.Sample.Api.Features;

using FluentValidation;
using PediatR;
using PediatR.Sample.Api.Domain;
using PediatR.Sample.Api.Security;

// Mode 1 — a classic hand-written command, unchanged from MediatR. [Authorize] sits on the request
// class; FluentValidation validates it; the handler publishes a notification for fan-out.
[Authorize(Roles = "user,admin")]
public sealed record CreateTodo(string Title) : IRequest<Todo>;

public sealed class CreateTodoValidator : AbstractValidator<CreateTodo>
{
    public CreateTodoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
    }
}

public sealed class CreateTodoHandler : IRequestHandler<CreateTodo, Todo>
{
    private readonly TodoStore _store;
    private readonly IPublisher _publisher;

    public CreateTodoHandler(TodoStore store, IPublisher publisher)
    {
        _store = store;
        _publisher = publisher;
    }

    public async Task<Todo> Handle(CreateTodo request, CancellationToken cancellationToken)
    {
        var todo = _store.Add(request.Title);
        await _publisher.Publish(new TodoCreated(todo.Id, todo.Title), cancellationToken);
        return todo;
    }
}
