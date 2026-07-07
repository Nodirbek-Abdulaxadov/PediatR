namespace PediatR;

/// <summary>
/// Marker interface for a streaming request that yields a sequence of
/// <typeparamref name="TResponse"/> values. Handled by an
/// <see cref="IStreamRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the elements produced by the stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
