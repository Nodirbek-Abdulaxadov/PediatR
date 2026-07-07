namespace PediatR.Entities;

using System;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Describes an open-generic behavior implementation together with the lifetime it should be
/// registered with.
/// </summary>
public class OpenBehavior
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenBehavior"/> class.
    /// </summary>
    /// <param name="openBehaviorType">The open-generic behavior implementation type.</param>
    /// <param name="serviceLifetime">The lifetime to register the behavior with.</param>
    public OpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        OpenBehaviorType = openBehaviorType;
        ServiceLifetime = serviceLifetime;
    }

    /// <summary>
    /// Gets the open-generic behavior implementation type.
    /// </summary>
    public Type OpenBehaviorType { get; }

    /// <summary>
    /// Gets the lifetime the behavior is registered with.
    /// </summary>
    public ServiceLifetime ServiceLifetime { get; }
}
