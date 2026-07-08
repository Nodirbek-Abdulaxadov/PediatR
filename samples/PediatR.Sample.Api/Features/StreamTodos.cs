namespace PediatR.Sample.Api.Features;

using System.Runtime.CompilerServices;
using PediatR;
using PediatR.Sample.Api.Domain;

// Streaming (IStreamRequest -> IAsyncEnumerable). Authored classically; coexists with the generator.
public sealed record StreamTodos : IStreamRequest<Todo>;

public sealed class StreamTodosHandler : IStreamRequestHandler<StreamTodos, Todo>
{
    private readonly TodoStore _store;

    public StreamTodosHandler(TodoStore store) => _store = store;

    public async IAsyncEnumerable<Todo> Handle(StreamTodos request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var todo in _store.All())
        {
            await Task.Delay(50, cancellationToken); // simulate item-by-item work
            yield return todo;
        }
    }
}
