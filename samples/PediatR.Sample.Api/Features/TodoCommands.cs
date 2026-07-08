namespace PediatR.Sample.Api.Features;

using PediatR;
using PediatR.Sample.Api.Domain;
using PediatR.Sample.Api.Security;

// Mode 3 (generated commands). DeleteTodo carries an [Authorize] that the generator forwards onto
// the generated DeleteTodoCommand, so AuthorizationBehaviour enforces it with no extra wiring.
public sealed class TodoCommands
{
    private readonly TodoStore _store;

    public TodoCommands(TodoStore store) => _store = store;

    // → CompleteTodoCommand(int Id) : ICommand<Todo?>
    [Command]
    public Task<Todo?> CompleteTodo(int id) => Task.FromResult(_store.Complete(id));

    // → [Authorize(Roles = "admin")] CompleteTodoCommand ... enforced by the pipeline.
    [Command]
    [Authorize(Roles = "admin")]
    public Task<bool> DeleteTodo(int id) => Task.FromResult(_store.Remove(id));
}
