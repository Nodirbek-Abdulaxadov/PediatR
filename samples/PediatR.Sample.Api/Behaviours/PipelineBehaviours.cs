namespace PediatR.Sample.Api.Behaviours;

using System.Diagnostics;
using System.Reflection;
using FluentValidation;
using PediatR;
using PediatR.Pipeline;
using PediatR.Sample.Api.Security;

// The same behavior set a Jason Taylor CleanArchitecture app wires up — running unchanged on PediatR.

/// <summary>Logs every request before it is handled (pre-processor, runs outermost).</summary>
public sealed class LoggingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger<LoggingPreProcessor<TRequest>> _logger;

    public LoggingPreProcessor(ILogger<LoggingPreProcessor<TRequest>> logger) => _logger = logger;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("→ {Request}", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}

public sealed class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehaviour(ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception exception) when (exception is not ValidationException and not UnauthorizedAccessException and not ForbiddenAccessException)
        {
            _logger.LogError(exception, "Unhandled exception for {Request}", typeof(TRequest).Name);
            throw;
        }
    }
}

public sealed class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _user;

    public AuthorizationBehaviour(ICurrentUser user) => _user = user;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var attributes = typeof(TRequest).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
        if (attributes.Length > 0)
        {
            if (_user.Id is null)
            {
                throw new UnauthorizedAccessException();
            }

            foreach (var attribute in attributes.Where(a => !string.IsNullOrWhiteSpace(a.Roles)))
            {
                var authorized = attribute.Roles.Split(',').Select(role => role.Trim()).Any(_user.IsInRole);
                if (!authorized)
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

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
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
    private readonly ILogger<PerformanceBehaviour<TRequest, TResponse>> _logger;

    public PerformanceBehaviour(ILogger<PerformanceBehaviour<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        timer.Stop();
        _logger.LogInformation("← {Request} in {Elapsed}ms", typeof(TRequest).Name, timer.ElapsedMilliseconds);
        return response;
    }
}
