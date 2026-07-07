namespace PediatR.Tests;

using System.Collections.Generic;

/// <summary>
/// Shared, singleton-registered sink used by test handlers, behaviors and processors to record the
/// order in which they run.
/// </summary>
public sealed class Recorder
{
    public List<string> Messages { get; } = new();
}
