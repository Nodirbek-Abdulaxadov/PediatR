namespace PediatR;

using System;

/// <summary>
/// Marks an instance method as a mediator handler. The PediatR source generator emits a matching
/// request record <c>{MethodName}Request : IRequest&lt;TResponse&gt;</c> and an
/// <see cref="IRequestHandler{TRequest, TResponse}"/> that delegates to the annotated method, injecting
/// the declaring type from DI.
/// </summary>
/// <remarks>
/// This is the neutral, CQRS-agnostic authoring mode. Use <see cref="CommandAttribute"/> or
/// <see cref="QueryAttribute"/> when you want the generated request to additionally carry the
/// <see cref="ICommand{TResponse}"/> / <see cref="IQuery{TResponse}"/> marker for behavior targeting.
/// The generated request keeps the exact MediatR shape (<see cref="IRequest{TResponse}"/>), so it runs
/// through the same pipeline as hand-written handlers.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class HandlerAttribute : Attribute
{
}
