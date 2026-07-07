namespace PediatR;

/// <summary>
/// Marker interface shared by every request. Allows a request to be referenced without
/// knowing its response type.
/// </summary>
public interface IBaseRequest
{
}

/// <summary>
/// Marker interface for a request that does not return a value.
/// Handled by an <see cref="IRequestHandler{TRequest}"/>.
/// </summary>
public interface IRequest : IBaseRequest
{
}

/// <summary>
/// Marker interface for a request that returns a response of type <typeparamref name="TResponse"/>.
/// Handled by an <see cref="IRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The type of response produced for this request.</typeparam>
public interface IRequest<out TResponse> : IBaseRequest
{
}
