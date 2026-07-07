namespace PediatR;

/// <summary>
/// Defines a mediator to encapsulate request/response and publishing interaction patterns.
/// Combines the sending surface (<see cref="ISender"/>) and the publishing surface
/// (<see cref="IPublisher"/>).
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
