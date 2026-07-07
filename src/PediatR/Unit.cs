namespace PediatR;

using System;
using System.Threading.Tasks;

/// <summary>
/// Represents a void type, since <see cref="System.Void"/> is not a usable type in C#.
/// Used as the response type for requests that do not return a meaningful value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    private static readonly Unit _value = new();

    /// <summary>
    /// The single, default value of <see cref="Unit"/>.
    /// </summary>
    public static ref readonly Unit Value => ref _value;

    /// <summary>
    /// A completed <see cref="System.Threading.Tasks.Task{TResult}"/> whose result is <see cref="Value"/>.
    /// </summary>
    public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(_value);

    /// <summary>
    /// Compares this value to another <see cref="Unit"/>. All units are equal, so this always returns 0.
    /// </summary>
    public int CompareTo(Unit other) => 0;

    int IComparable.CompareTo(object? obj) => 0;

    /// <summary>
    /// Returns a hash code for this value. All units share the hash code 0.
    /// </summary>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Determines whether the specified <see cref="Unit"/> equals this value. Always <see langword="true"/>.
    /// </summary>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines whether the specified object is a <see cref="Unit"/>.
    /// </summary>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Determines whether two units are equal. Always <see langword="true"/>.
    /// </summary>
    public static bool operator ==(Unit first, Unit second) => true;

    /// <summary>
    /// Determines whether two units are unequal. Always <see langword="false"/>.
    /// </summary>
    public static bool operator !=(Unit first, Unit second) => false;

    /// <summary>
    /// Returns the string representation of a unit value, <c>()</c>.
    /// </summary>
    public override string ToString() => "()";
}
