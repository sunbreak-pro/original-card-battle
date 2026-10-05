// Polyfill for C# 9 `init` accessors / records when targeting netstandard2.1 (a copy of BattleCore's,
// whose type is internal and so not seen from this assembly).
// The compiler requires System.Runtime.CompilerServices.IsExternalInit to exist,
// but netstandard2.1 (and Unity's Mono) does not ship it. Defining it here is the
// standard, documented workaround and lets these files compile unchanged in Unity.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
