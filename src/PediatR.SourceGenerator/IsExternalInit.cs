// Enables positional records / `init`-only setters on netstandard2.0, whose reference assemblies do
// not ship System.Runtime.CompilerServices.IsExternalInit. On net5.0+ the type already exists.
#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
