namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Controls when registered <see cref="PediatR.Pipeline.IRequestExceptionAction{TRequest, TException}"/>
/// instances run relative to exception handlers.
/// </summary>
public enum RequestExceptionActionProcessorStrategy
{
    /// <summary>
    /// Exception actions run only for exceptions that were not recovered by an exception handler.
    /// </summary>
    ApplyForUnhandledExceptions,

    /// <summary>
    /// Exception actions run for every exception, whether or not it was recovered by a handler.
    /// </summary>
    ApplyForAllExceptions
}
