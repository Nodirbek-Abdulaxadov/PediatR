// Enables `init`-only setters and positional records on target frameworks (netstandard2.0) whose
// reference assemblies do not ship System.Runtime.CompilerServices.IsExternalInit. On net5.0+ the
// type already exists, so this shim is compiled out.
#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Reserved for use by the compiler to support <see langword="init"/>-only setters.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
