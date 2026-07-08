namespace PediatR.Sample.Api.Features;

using PediatR;
using PediatR.Sample.Api.Domain;

// Modes 2 & 3 (generated). One method per use case; the generator emits the request record, the
// handler (which injects this class from DI), and an ergonomic ISender extension per method.
public sealed class TodoQueries
{
    private readonly TodoStore _store;

    public TodoQueries(TodoStore store) => _store = store;

    // → GetTodoQuery(int Id) : IQuery<Todo?> ; sender.GetTodo(id)
    [Query]
    public Task<Todo?> GetTodo(int id) => Task.FromResult(_store.Get(id));

    // → ListTodosQuery() : IQuery<IReadOnlyList<Todo>> ; sender.ListTodos()
    [Query]
    public Task<IReadOnlyList<Todo>> ListTodos() => Task.FromResult(_store.All());

    // Neutral marker. → CountTodosRequest() : IRequest<int> ; sender.CountTodos()
    [Handler]
    public Task<int> CountTodos() => Task.FromResult(_store.Count);
}
