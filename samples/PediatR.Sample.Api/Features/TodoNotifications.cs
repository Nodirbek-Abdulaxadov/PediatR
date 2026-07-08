namespace PediatR.Sample.Api.Features;

using PediatR;

// One notification, two handlers — PediatR publishes to both (fan-out).
public sealed record TodoCreated(int Id, string Title) : INotification;

public sealed class AuditTodoCreatedHandler : INotificationHandler<TodoCreated>
{
    private readonly ILogger<AuditTodoCreatedHandler> _logger;

    public AuditTodoCreatedHandler(ILogger<AuditTodoCreatedHandler> logger) => _logger = logger;

    public Task Handle(TodoCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("AUDIT: todo #{Id} created", notification.Id);
        return Task.CompletedTask;
    }
}

public sealed class EmailTodoCreatedHandler : INotificationHandler<TodoCreated>
{
    private readonly ILogger<EmailTodoCreatedHandler> _logger;

    public EmailTodoCreatedHandler(ILogger<EmailTodoCreatedHandler> logger) => _logger = logger;

    public Task Handle(TodoCreated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("EMAIL: notifying subscribers about '{Title}'", notification.Title);
        return Task.CompletedTask;
    }
}
