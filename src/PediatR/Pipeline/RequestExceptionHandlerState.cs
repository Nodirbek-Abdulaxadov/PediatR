namespace PediatR.Pipeline;

/// <summary>
/// Tracks whether an exception thrown during request handling has been handled, and carries the
/// response supplied by the handler.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public class RequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// Gets a value indicating whether the exception has been handled.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// Gets the response supplied when the exception was handled.
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Marks the exception as handled and records the response to return in place of the exception.
    /// </summary>
    /// <param name="response">The response to return.</param>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
