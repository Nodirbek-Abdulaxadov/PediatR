namespace PediatR;

using System;

/// <summary>
/// Marks an instance method as a query handler. The PediatR source generator emits a matching
/// request record <c>{MethodName}Query : IQuery&lt;TResponse&gt;</c> and an
/// <see cref="IRequestHandler{TRequest, TResponse}"/> that delegates to the annotated method, injecting
/// the declaring type from DI.
/// </summary>
/// <remarks>
/// Because the generated request implements <see cref="IQuery{TResponse}"/> (which extends
/// <see cref="IRequest{TResponse}"/>), it can be targeted by query-only pipeline behaviors (for example a
/// caching behavior) while still flowing through the standard pipeline. This marker carries no caching
/// semantics on its own — caching is a separate, opt-in behavior.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class QueryAttribute : Attribute
{
}
