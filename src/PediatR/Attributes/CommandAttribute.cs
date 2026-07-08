namespace PediatR;

using System;

/// <summary>
/// Marks an instance method as a command handler. The PediatR source generator emits a matching
/// request record <c>{MethodName}Command : ICommand&lt;TResponse&gt;</c> and an
/// <see cref="IRequestHandler{TRequest, TResponse}"/> that delegates to the annotated method, injecting
/// the declaring type from DI.
/// </summary>
/// <remarks>
/// Because the generated request implements <see cref="ICommand{TResponse}"/> (which extends
/// <see cref="IRequest{TResponse}"/>), it can be targeted by command-only pipeline behaviors while still
/// flowing through the standard pipeline.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
}
