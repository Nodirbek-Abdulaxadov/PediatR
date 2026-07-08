namespace PediatR.Sample.Api.Domain;

public sealed record Todo(int Id, string Title, bool Done);

/// <summary>A thread-safe in-memory store — stands in for a repository / DbContext.</summary>
public sealed class TodoStore
{
    private readonly Dictionary<int, Todo> _items = new();
    private readonly object _gate = new();
    private int _next = 1;

    public Todo Add(string title)
    {
        lock (_gate)
        {
            var todo = new Todo(_next++, title, Done: false);
            _items[todo.Id] = todo;
            return todo;
        }
    }

    public Todo? Get(int id)
    {
        lock (_gate)
        {
            return _items.TryGetValue(id, out var todo) ? todo : null;
        }
    }

    public IReadOnlyList<Todo> All()
    {
        lock (_gate)
        {
            return _items.Values.OrderBy(t => t.Id).ToList();
        }
    }

    public Todo? Complete(int id)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(id, out var todo))
            {
                return null;
            }

            var done = todo with { Done = true };
            _items[id] = done;
            return done;
        }
    }

    public bool Remove(int id)
    {
        lock (_gate)
        {
            return _items.Remove(id);
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }
}
